using FlowMeet.Server.Data;
using FlowMeet.Server.Models.DTOs;
using FlowMeet.Server.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace FlowMeet.Server.Services;

public class UserStateService : IUserStateService
{
    private const int DefaultMoodLevel = 3;
    private const int SeedPreviousDayRawBalance = 35;
    private const int HistoryDays = 14;

    private readonly AppDbContext _context;

    public UserStateService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<(int ResourceLevel, string Message)> SetMoodAsync(Guid userId, MoodRequest request)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var state = await _context.UserStates
            .FirstOrDefaultAsync(us => us.UserId == userId && us.Date == today);

        if (state == null)
        {
            state = new UserState
            {
                UserId = userId,
                Date = today
            };
            _context.UserStates.Add(state);
        }

        state.MoodLevel = request.MoodLevel;
        state.SleepQuality = request.SleepQuality;
        state.BackgroundLoadLevel = request.BackgroundLoadLevel;

        await _context.SaveChangesAsync();

        var currentRawBalance = await CalculateRawBalanceAtAsync(userId, DateTime.UtcNow);
        state.RawBalance = currentRawBalance;
        state.ResourceLevel = ClampVisibleResource(currentRawBalance);
        await _context.SaveChangesAsync();

        return (state.ResourceLevel, "Состояние дня сохранено");
    }

    public async Task<ResourceResponse> GetResourceStatusAsync(Guid userId)
    {
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var state = await _context.UserStates
            .AsNoTracking()
            .FirstOrDefaultAsync(us => us.UserId == userId && us.Date == today);

        var rawBalance = await CalculateRawBalanceAtAsync(userId, now);
        var visibleResource = ClampVisibleResource(rawBalance);

        string msg = visibleResource switch
        {
            >= 85 => "Высокий ресурс, можно планировать активные встречи",
            >= 60 => "Нормальный ресурс, день выглядит устойчиво",
            >= 35 => "Ресурс снижен, лучше не перегружать себя",
            _ => "Ресурс на исходе, стоит дать себе паузу"
        };

        return new ResourceResponse
        {
            ResourceLevel = visibleResource,
            RawBalance = rawBalance,
            StatusMessage = msg,
            MoodLevel = state?.MoodLevel ?? DefaultMoodLevel,
            SleepQuality = state?.SleepQuality ?? SleepQuality.Normal,
            BackgroundLoadLevel = state?.BackgroundLoadLevel ?? BackgroundLoadLevel.Calm
        };
    }

    public async Task<int> GetProjectedResourceAsync(Guid userId, DateTime momentUtc)
    {
        var resources = await GetProjectedResourcesAsync(userId, new[] { momentUtc });
        return resources.Values.FirstOrDefault();
    }

    public async Task<Dictionary<DateTime, int>> GetProjectedResourcesAsync(Guid userId, IReadOnlyCollection<DateTime> momentsUtc)
    {
        var rawBalances = await CalculateProjectedRawBalancesAsync(userId, momentsUtc);
        return rawBalances.ToDictionary(pair => pair.Key, pair => ClampVisibleResource(pair.Value));
    }

    private async Task<Dictionary<DateTime, int>> CalculateProjectedRawBalancesAsync(Guid userId, IReadOnlyCollection<DateTime> momentsUtc)
    {
        var normalizedMoments = momentsUtc
            .Select(moment => moment.ToUniversalTime())
            .Distinct()
            .OrderBy(moment => moment)
            .ToList();

        if (!normalizedMoments.Any())
            return new Dictionary<DateTime, int>();

        var firstTargetDate = DateOnly.FromDateTime(normalizedMoments[0]);
        var lastTargetDate = DateOnly.FromDateTime(normalizedMoments[^1]);
        var historyStart = firstTargetDate.AddDays(-HistoryDays);
        var historyStartUtc = historyStart.ToDateTime(TimeOnly.MinValue).ToUniversalTime();
        var historyEndUtc = lastTargetDate.AddDays(1).ToDateTime(TimeOnly.MinValue).ToUniversalTime();

        var states = await _context.UserStates
            .AsNoTracking()
            .Where(us => us.UserId == userId && us.Date >= historyStart && us.Date <= lastTargetDate)
            .ToDictionaryAsync(us => us.Date);

        var events = await _context.Events
            .AsNoTracking()
            .Where(e => e.UserId == userId && e.StartTime < historyEndUtc && e.EndTime > historyStartUtc)
            .ToListAsync();

        var meetings = await _context.Meetings
            .AsNoTracking()
            .Include(m => m.Participants)
            .Where(m => m.Status == MeetingStatus.Confirmed
                        && m.StartTime < historyEndUtc
                        && (m.InitiatorId == userId
                            || m.Participants.Any(p => p.UserId == userId && p.Status == ParticipantStatus.Accepted)))
            .ToListAsync();

        var baseScheduleEntries = await _context.BaseScheduleEntries
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .ToListAsync();

        var baseScheduleExceptions = await _context.BaseScheduleOccurrenceExceptions
            .AsNoTracking()
            .Where(exception => exception.UserId == userId
                                && exception.Date >= historyStart
                                && exception.Date <= lastTargetDate)
            .ToListAsync();

        var momentsByDate = normalizedMoments
            .GroupBy(moment => DateOnly.FromDateTime(moment))
            .ToDictionary(group => group.Key, group => group.OrderBy(moment => moment).ToList());

        var projectedRawBalances = new Dictionary<DateTime, int>();
        var previousDayRawBalance = SeedPreviousDayRawBalance;

        for (var date = historyStart; date <= lastTargetDate; date = date.AddDays(1))
        {
            var state = states.GetValueOrDefault(date);
            var moodLevel = state?.MoodLevel ?? DefaultMoodLevel;
            var sleepQuality = state?.SleepQuality ?? SleepQuality.Normal;
            var backgroundLoad = state?.BackgroundLoadLevel ?? BackgroundLoadLevel.Calm;

            var morningRawBalance = Math.Min(GetMorningResourceLimit(sleepQuality), previousDayRawBalance + GetSleepRecovery(sleepQuality));
            if (momentsByDate.TryGetValue(date, out var dateMoments))
            {
                foreach (var moment in dateMoments)
                {
                    var eventCostAtMoment = CalculateEventCostsForWindow(date, moment, userId, events, meetings, baseScheduleEntries, baseScheduleExceptions);
                    var backgroundCostAtMoment = GetBackgroundCost(backgroundLoad, date, moment);
                    var moodAdjustmentAtMoment = GetMoodAdjustment(moodLevel);
                    var rawBalanceAtMoment = morningRawBalance + moodAdjustmentAtMoment - backgroundCostAtMoment - (int)Math.Round(eventCostAtMoment);
                    projectedRawBalances[moment] = Math.Clamp(rawBalanceAtMoment, -120, 100);
                }
            }

            var endOfDayUtc = date.AddDays(1).ToDateTime(TimeOnly.MinValue).ToUniversalTime();
            var eventCost = CalculateEventCostsForWindow(date, endOfDayUtc, userId, events, meetings, baseScheduleEntries, baseScheduleExceptions);
            var backgroundCost = GetBackgroundCost(backgroundLoad, date, endOfDayUtc);
            var moodAdjustment = GetMoodAdjustment(moodLevel);

            var currentRawBalance = morningRawBalance + moodAdjustment - backgroundCost - (int)Math.Round(eventCost);
            previousDayRawBalance = Math.Clamp(currentRawBalance, -120, 100);
        }

        return projectedRawBalances;
    }

    private async Task<int> CalculateRawBalanceAtAsync(Guid userId, DateTime momentUtc)
    {
        var resourceMap = await CalculateProjectedRawBalancesAsync(userId, new[] { momentUtc });
        var normalizedMoment = momentUtc.ToUniversalTime();
        return resourceMap.TryGetValue(normalizedMoment, out var resourceLevel)
            ? resourceLevel
            : 0;
    }

    private static double CalculateEventCostsForWindow(
        DateOnly date,
        DateTime evaluationEndUtc,
        Guid userId,
        List<Event> events,
        List<Meeting> meetings,
        List<BaseScheduleEntry> baseScheduleEntries,
        List<BaseScheduleOccurrenceException> baseScheduleExceptions)
    {
        var startOfDayUtc = date.ToDateTime(TimeOnly.MinValue).ToUniversalTime();
        if (evaluationEndUtc <= startOfDayUtc)
            return 0;

        var endOfWindowUtc = evaluationEndUtc;
        var combinedEvents = new List<Event>();

        combinedEvents.AddRange(events.Where(e => e.StartTime < endOfWindowUtc && e.EndTime > startOfDayUtc));

        var currentDayOfWeek = (int)date.DayOfWeek;
        combinedEvents.AddRange(baseScheduleEntries
            .Where(entry => entry.DayOfWeek == currentDayOfWeek
                            && entry.EffectiveFromDate <= date
                            && (entry.EffectiveToDate == null || entry.EffectiveToDate > date)
                            && !baseScheduleExceptions.Any(exception =>
                                exception.BaseScheduleEntryId == entry.Id
                                && exception.Date == date))
            .Select(entry => new Event
            {
                Id = Guid.NewGuid(),
                UserId = entry.UserId,
                Title = entry.Title,
                Description = entry.Description,
                StartTime = date.ToDateTime(TimeOnly.FromTimeSpan(entry.StartTime)).ToUniversalTime(),
                EndTime = date.ToDateTime(TimeOnly.FromTimeSpan(entry.EndTime)).ToUniversalTime(),
                Type = entry.Type
            })
            .Where(entry => entry.StartTime < endOfWindowUtc && entry.EndTime > startOfDayUtc));

        combinedEvents.AddRange(meetings
            .Where(meeting => CountsTowardsUserSchedule(meeting, userId))
            .Select(meeting => new Event
            {
                Id = meeting.Id,
                UserId = userId,
                Title = meeting.Title,
                Description = meeting.Description,
                StartTime = meeting.StartTime,
                EndTime = meeting.StartTime.Add(meeting.Duration),
                Type = EventType.Mandatory
            })
            .Where(entry => entry.StartTime < endOfWindowUtc && entry.EndTime > startOfDayUtc));

        return combinedEvents.Sum(ev =>
        {
            var overlapStart = ev.StartTime > startOfDayUtc ? ev.StartTime : startOfDayUtc;
            var overlapEnd = ev.EndTime < endOfWindowUtc ? ev.EndTime : endOfWindowUtc;
            if (overlapEnd <= overlapStart)
                return 0;

            var overlapHours = (overlapEnd - overlapStart).TotalHours;
            return overlapHours * GetEventHourlyCost(ev.Type);
        });
    }

    private static int GetSleepRecovery(SleepQuality quality) => quality switch
    {
        SleepQuality.Poor => 30,
        SleepQuality.Normal => 50,
        SleepQuality.Good => 65,
        _ => 50
    };

    private static int GetMorningResourceLimit(SleepQuality quality) => quality switch
    {
        SleepQuality.Poor => 65,
        SleepQuality.Normal => 85,
        _ => 100
    };

    private static int GetMoodAdjustment(int moodLevel) => moodLevel switch
    {
        1 => -12,
        2 => -6,
        3 => 0,
        4 => 4,
        5 => 8,
        _ => 0
    };

    private static int GetBackgroundCost(BackgroundLoadLevel load, DateOnly date, DateTime evaluationEndUtc)
    {
        var dayStartUtc = date.ToDateTime(TimeOnly.MinValue).ToUniversalTime();
        var elapsedHours = Math.Clamp((evaluationEndUtc - dayStartUtc).TotalHours, 0, 24);
        var progress = elapsedHours / 24d;
        var fullDayCost = load switch
        {
            BackgroundLoadLevel.Calm => 0,
            BackgroundLoadLevel.Tense => 10,
            BackgroundLoadLevel.Heavy => 20,
            _ => 0
        };

        return (int)Math.Round(fullDayCost * progress);
    }

    private static double GetEventHourlyCost(EventType type) => type switch
    {
        EventType.Mandatory => 14,
        EventType.Flexible => 8,
        EventType.Desirable => 4,
        _ => 8
    };

    private static bool CountsTowardsUserSchedule(Meeting meeting, Guid userId) =>
        meeting.InitiatorId == userId
        || meeting.Participants.Any(p => p.UserId == userId && p.Status == ParticipantStatus.Accepted);

    private static int ClampVisibleResource(int rawBalance) => Math.Clamp(rawBalance, 0, 100);
}
