using FlowMeet.Server.Data;
using FlowMeet.Server.Models.DTOs;
using FlowMeet.Server.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace FlowMeet.Server.Services;

public class DashboardInsightsService : IDashboardInsightsService
{
    private readonly AppDbContext _context;
    private readonly IUserStateService _userStateService;
    private readonly IPlanningService _planningService;

    public DashboardInsightsService(
        AppDbContext context,
        IUserStateService userStateService,
        IPlanningService planningService)
    {
        _context = context;
        _userStateService = userStateService;
        _planningService = planningService;
    }

    public async Task<DashboardInsightsResponse> GetInsightsAsync(Guid userId)
    {
        var nowUtc = DateTime.UtcNow;
        var todayLocal = DateOnly.FromDateTime(DateTime.Now);
        var last14Days = todayLocal.AddDays(-13);
        var last30DaysUtc = nowUtc.AddDays(-30);

        var states = await _context.UserStates
            .AsNoTracking()
            .Where(state => state.UserId == userId && state.Date >= last14Days && state.Date <= todayLocal)
            .ToListAsync();

        var involvedMeetings = await _context.Meetings
            .AsNoTracking()
            .Include(meeting => meeting.Initiator)
            .Include(meeting => meeting.RelatedGroup)
            .Include(meeting => meeting.Participants)
            .ThenInclude(participant => participant.User)
            .Where(meeting => meeting.Status == MeetingStatus.Confirmed
                              && (meeting.InitiatorId == userId
                                  || meeting.Participants.Any(participant => participant.UserId == userId
                                                                             && participant.Status == ParticipantStatus.Accepted)))
            .ToListAsync();

        var pastMeetings = involvedMeetings
            .Where(meeting => meeting.StartTime.Add(meeting.Duration) <= nowUtc)
            .OrderByDescending(meeting => meeting.StartTime)
            .ToList();

        var futureMeetings = involvedMeetings
            .Where(meeting => meeting.StartTime.Add(meeting.Duration) > nowUtc)
            .OrderBy(meeting => meeting.StartTime)
            .ToList();

        var friends = await GetAcceptedFriendsAsync(userId);
        var participantStats = BuildFrequentParticipantStats(userId, pastMeetings);
        var resource = await _userStateService.GetResourceStatusAsync(userId);

        var response = new DashboardInsightsResponse
        {
            MeetingsLast30Days = pastMeetings.Count(meeting => meeting.StartTime >= last30DaysUtc),
            TotalPastMeetings = pastMeetings.Count,
            ConfirmedFutureMeetings = futureMeetings.Count,
            AverageMoodLast14Days = states.Count == 0 ? null : Math.Round(states.Average(state => state.MoodLevel), 1),
            AverageResourceLast14Days = states.Count == 0 ? null : Math.Round(states.Average(state => state.ResourceLevel), 1),
            FrequentParticipants = participantStats.Take(3).ToList(),
            OverloadWarning = await BuildOverloadWarningAsync(userId, resource.ResourceLevel, todayLocal, nowUtc)
        };

        response.Recommendations = await BuildRecommendationsAsync(
            userId,
            friends,
            participantStats,
            pastMeetings,
            states,
            resource.ResourceLevel,
            todayLocal,
            nowUtc);

        return response;
    }

    private async Task<List<User>> GetAcceptedFriendsAsync(Guid userId)
    {
        var friendships = await _context.Friendships
            .AsNoTracking()
            .Include(friendship => friendship.Requester)
            .Include(friendship => friendship.Addressee)
            .Where(friendship => friendship.Status == FriendshipStatus.Accepted
                                 && (friendship.RequesterId == userId || friendship.AddresseeId == userId))
            .ToListAsync();

        return friendships
            .Select(friendship => friendship.RequesterId == userId ? friendship.Addressee : friendship.Requester)
            .Where(friend => friend != null)
            .Select(friend => friend!)
            .OrderBy(friend => friend.FirstName)
            .ThenBy(friend => friend.LastName)
            .ToList();
    }

    private static List<FrequentParticipantDto> BuildFrequentParticipantStats(Guid userId, IEnumerable<Meeting> pastMeetings)
    {
        return pastMeetings
            .SelectMany(meeting => GetCounterpartUsers(meeting, userId).Select(user => new { Meeting = meeting, User = user }))
            .GroupBy(item => item.User.Id)
            .Select(group =>
            {
                var lastMeeting = group.Max(item => item.Meeting.StartTime);
                var user = group.First().User;
                return new FrequentParticipantDto
                {
                    UserId = user.Id,
                    Name = BuildUserName(user),
                    MeetingsCount = group.Count(),
                    LastMeetingAt = lastMeeting
                };
            })
            .OrderByDescending(item => item.MeetingsCount)
            .ThenByDescending(item => item.LastMeetingAt)
            .ToList();
    }

    private async Task<List<DashboardRecommendationDto>> BuildRecommendationsAsync(
        Guid userId,
        List<User> friends,
        List<FrequentParticipantDto> participantStats,
        List<Meeting> pastMeetings,
        List<UserState> states,
        int currentResource,
        DateOnly todayLocal,
        DateTime nowUtc)
    {
        var recommendations = new List<DashboardRecommendationDto>();
        var recommendedUserIds = new HashSet<Guid>();

        AddLongTimeNoSeeRecommendations(friends, participantStats, nowUtc, recommendations, recommendedUserIds);
        AddGoodMoodRepeatRecommendation(userId, pastMeetings, states, recommendations, recommendedUserIds);

        if (currentResource >= 70)
        {
            await AddHighEnergyRecommendationAsync(
                userId,
                friends,
                todayLocal,
                nowUtc,
                recommendations,
                recommendedUserIds);
        }

        return recommendations.Take(5).ToList();
    }

    private static void AddLongTimeNoSeeRecommendations(
        IEnumerable<User> friends,
        IReadOnlyCollection<FrequentParticipantDto> participantStats,
        DateTime nowUtc,
        List<DashboardRecommendationDto> recommendations,
        HashSet<Guid> recommendedUserIds)
    {
        foreach (var friend in friends)
        {
            var stats = participantStats.FirstOrDefault(item => item.UserId == friend.Id);
            var lastMeetingAt = stats?.LastMeetingAt;
            var daysSinceLastMeeting = lastMeetingAt.HasValue
                ? (nowUtc - lastMeetingAt.Value).TotalDays
                : double.PositiveInfinity;

            if (daysSinceLastMeeting < 21)
                continue;

            recommendations.Add(new DashboardRecommendationDto
            {
                Type = "LongTimeNoSee",
                Title = "Вы давно не виделись",
                Message = lastMeetingAt.HasValue
                    ? $"С {BuildUserName(friend)} не было встреч около {Math.Round(daysSinceLastMeeting)} дней. Можно подобрать спокойное окно на этой неделе."
                    : $"С {BuildUserName(friend)} еще не было ни одной встречи. Самое время для первой встречи.",
                UserId = friend.Id,
                UserName = BuildUserName(friend)
            });

            recommendedUserIds.Add(friend.Id);

            if (recommendations.Count >= 2)
                return;
        }
    }

    private static void AddGoodMoodRepeatRecommendation(
        Guid userId,
        List<Meeting> pastMeetings,
        List<UserState> states,
        List<DashboardRecommendationDto> recommendations,
        HashSet<Guid> recommendedUserIds)
    {
        var goodMoodDates = states
            .Where(state => state.MoodLevel >= 4)
            .Select(state => state.Date)
            .ToHashSet();

        if (goodMoodDates.Count == 0)
            return;

        var candidate = pastMeetings
            .Where(meeting => goodMoodDates.Contains(DateOnly.FromDateTime(meeting.StartTime.ToLocalTime())))
            .SelectMany(meeting => GetCounterpartUsers(meeting, userId).Select(user => new { Meeting = meeting, User = user }))
            .Where(item => !recommendedUserIds.Contains(item.User.Id))
            .GroupBy(item => item.User.Id)
            .Select(group => new
            {
                User = group.First().User,
                Count = group.Count(),
                LastMeetingAt = group.Max(item => item.Meeting.StartTime)
            })
            .OrderByDescending(item => item.Count)
            .ThenByDescending(item => item.LastMeetingAt)
            .FirstOrDefault();

        if (candidate == null)
            return;

        recommendations.Add(new DashboardRecommendationDto
        {
            Type = "GoodMoodRepeat",
            Title = "После этих встреч настроение было выше",
            Message = $"После встреч с {BuildUserName(candidate.User)} у вас часто было хорошее настроение. Возможно, стоит повторить.",
            UserId = candidate.User.Id,
            UserName = BuildUserName(candidate.User)
        });

        recommendedUserIds.Add(candidate.User.Id);
    }

    private async Task AddHighEnergyRecommendationAsync(
        Guid userId,
        IEnumerable<User> friends,
        DateOnly todayLocal,
        DateTime nowUtc,
        List<DashboardRecommendationDto> recommendations,
        HashSet<Guid> recommendedUserIds)
    {
        foreach (var friend in friends.Where(friend => !recommendedUserIds.Contains(friend.Id)))
        {
            var friendResource = await _userStateService.GetProjectedResourceAsync(friend.Id, nowUtc);
            if (friendResource < 70)
                continue;

            var planningResult = await _planningService.FindGroupSlotsAsync(userId, new List<Guid> { friend.Id }, todayLocal, 60);
            var todaySlot = planningResult.Slots.FirstOrDefault(slot => DateOnly.FromDateTime(slot.StartTime.ToLocalTime()) == todayLocal);

            if (!planningResult.IsSuccess || todaySlot == null)
                continue;

            recommendations.Add(new DashboardRecommendationDto
            {
                Type = "HighEnergyFreeToday",
                Title = "Сегодня есть ресурс и свободное окно",
                Message = $"У вас и {BuildUserName(friend)} высокий ресурс. Сегодня можно встретиться в {todaySlot.StartTime.ToLocalTime():HH:mm}.",
                UserId = friend.Id,
                UserName = BuildUserName(friend),
                SuggestedStartTime = todaySlot.StartTime,
                SuggestedEndTime = todaySlot.EndTime
            });

            return;
        }
    }

    private async Task<string?> BuildOverloadWarningAsync(Guid userId, int resourceLevel, DateOnly todayLocal, DateTime nowUtc)
    {
        if (resourceLevel < 35)
            return "Ресурс сегодня снижен: лучше не добавлять новые встречи.";

        var todayStartLocal = todayLocal.ToDateTime(TimeOnly.MinValue);
        var tomorrowStartLocal = todayLocal.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var todayStartUtc = todayStartLocal.ToUniversalTime();
        var tomorrowStartUtc = tomorrowStartLocal.ToUniversalTime();

        var mandatoryEvents = await _context.Events
            .AsNoTracking()
            .Where(ev => ev.UserId == userId
                         && ev.Type == EventType.Mandatory
                         && ev.StartTime < tomorrowStartUtc
                         && ev.EndTime > todayStartUtc)
            .ToListAsync();

        var confirmedMeetings = await _context.Meetings
            .AsNoTracking()
            .Include(meeting => meeting.Participants)
            .Where(meeting => meeting.Status == MeetingStatus.Confirmed
                              && meeting.StartTime < tomorrowStartUtc
                              && (meeting.InitiatorId == userId
                                  || meeting.Participants.Any(participant => participant.UserId == userId
                                                                             && participant.Status == ParticipantStatus.Accepted)))
            .ToListAsync();

        confirmedMeetings = confirmedMeetings
            .Where(meeting => meeting.StartTime.Add(meeting.Duration) > todayStartUtc)
            .ToList();

        var currentDayOfWeek = (int)todayLocal.DayOfWeek;
        var mandatoryBaseEntries = await _context.BaseScheduleEntries
            .AsNoTracking()
            .Where(entry => entry.UserId == userId
                            && entry.Type == EventType.Mandatory
                            && entry.DayOfWeek == currentDayOfWeek
                            && entry.EffectiveFromDate <= todayLocal
                            && (entry.EffectiveToDate == null || entry.EffectiveToDate > todayLocal))
            .ToListAsync();

        var mandatoryHours =
            mandatoryEvents.Sum(ev => GetOverlapHours(ev.StartTime, ev.EndTime, todayStartUtc, tomorrowStartUtc))
            + confirmedMeetings.Sum(meeting => GetOverlapHours(meeting.StartTime, meeting.StartTime.Add(meeting.Duration), todayStartUtc, tomorrowStartUtc))
            + mandatoryBaseEntries.Sum(entry => (entry.EndTime - entry.StartTime).TotalHours);

        if (mandatoryHours >= 6)
            return $"Сегодня уже около {Math.Round(mandatoryHours, 1)} часов обязательной нагрузки. Новые встречи лучше пока не планировать.";

        return null;
    }

    private static double GetOverlapHours(DateTime startUtc, DateTime endUtc, DateTime rangeStartUtc, DateTime rangeEndUtc)
    {
        var start = startUtc > rangeStartUtc ? startUtc : rangeStartUtc;
        var end = endUtc < rangeEndUtc ? endUtc : rangeEndUtc;
        return end <= start ? 0 : (end - start).TotalHours;
    }

    private static IEnumerable<User> GetCounterpartUsers(Meeting meeting, Guid userId)
    {
        if (meeting.InitiatorId != userId && meeting.Initiator != null)
            yield return meeting.Initiator;

        foreach (var participant in meeting.Participants)
        {
            if (participant.UserId == userId || participant.Status != ParticipantStatus.Accepted || participant.User == null)
                continue;

            yield return participant.User;
        }
    }

    private static string BuildUserName(User user)
    {
        var fullName = $"{user.FirstName} {user.LastName}".Trim();
        return string.IsNullOrWhiteSpace(fullName) ? user.Email : fullName;
    }
}
