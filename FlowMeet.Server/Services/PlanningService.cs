using System.Text.Json;
using FlowMeet.Server.Data;
using FlowMeet.Server.Models.DTOs;
using FlowMeet.Server.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace FlowMeet.Server.Services;

public class PlanningService : IPlanningService
{
    private static readonly JsonSerializerOptions SettingsJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly AppDbContext _context;
    private readonly IUserStateService _userStateService;

    public PlanningService(AppDbContext context, IUserStateService userStateService)
    {
        _context = context;
        _userStateService = userStateService;
    }

    public async Task<(bool IsSuccess, string ErrorMessage, List<TimeSlotDto> Slots)> FindGroupSlotsAsync(Guid currentUserId, List<Guid> participantIds, DateOnly startDate, int durationMinutes)
    {
        if (durationMinutes <= 0)
            return (false, "Длительность встречи должна быть положительной", new List<TimeSlotDto>());

        var distinctParticipantIds = participantIds
            .Append(currentUserId)
            .Distinct()
            .ToList();

        var participants = await _context.Users
            .Where(u => distinctParticipantIds.Contains(u.Id))
            .ToListAsync();

        var existingUserIds = participants.Select(u => u.Id).ToList();
        var missingUserIds = distinctParticipantIds.Except(existingUserIds).ToList();
        if (missingUserIds.Any())
            return (false, "Один или несколько участников не найдены", new List<TimeSlotDto>());

        var otherParticipantIds = distinctParticipantIds
            .Where(id => id != currentUserId)
            .ToList();

        if (otherParticipantIds.Any())
        {
            var friendIds = await _context.Friendships
                .Where(f => f.Status == FriendshipStatus.Accepted
                            && (f.RequesterId == currentUserId || f.AddresseeId == currentUserId))
                .Select(f => f.RequesterId == currentUserId ? f.AddresseeId : f.RequesterId)
                .ToListAsync();

            var currentUserGroupIds = await _context.GroupMembers
                .Where(gm => gm.UserId == currentUserId)
                .Select(gm => gm.GroupId)
                .ToListAsync();

            var sharedGroupUserIds = await _context.GroupMembers
                .Where(gm => currentUserGroupIds.Contains(gm.GroupId))
                .Select(gm => gm.UserId)
                .Distinct()
                .ToListAsync();

            var allowedUserIds = friendIds
                .Concat(sharedGroupUserIds)
                .Distinct()
                .ToHashSet();

            var unauthorizedUserIds = otherParticipantIds
                .Where(id => !allowedUserIds.Contains(id))
                .ToList();

            if (unauthorizedUserIds.Any())
                return (false, "Можно планировать только с друзьями или участниками общих групп", new List<TimeSlotDto>());
        }

        var settingsByUserId = participants.ToDictionary(
            user => user.Id,
            user => ParseSettings(user.SettingsJson));

        var startRange = startDate.ToDateTime(new TimeOnly(0, 0)).ToUniversalTime();
        var endRange = startRange.AddDays(7);
        var earliestFutureStartUtc = RoundUpToStep(DateTime.UtcNow, 30);

        var allEvents = await _context.Events
            .Where(e => distinctParticipantIds.Contains(e.UserId)
                        && e.StartTime < endRange
                        && e.EndTime > startRange)
            .ToListAsync();

        var meetings = await _context.Meetings
            .AsNoTracking()
            .Include(m => m.Participants)
            .Where(m => m.Status == MeetingStatus.Confirmed
                        && m.StartTime < endRange
                        && (distinctParticipantIds.Contains(m.InitiatorId)
                            || m.Participants.Any(p => distinctParticipantIds.Contains(p.UserId)
                                                       && p.Status == ParticipantStatus.Accepted)))
            .ToListAsync();

        var baseScheduleEntries = await _context.BaseScheduleEntries
            .Where(e => distinctParticipantIds.Contains(e.UserId))
            .ToListAsync();

        var baseScheduleExceptions = await _context.BaseScheduleOccurrenceExceptions
            .AsNoTracking()
            .Where(exception => distinctParticipantIds.Contains(exception.UserId)
                                && exception.Date >= startDate
                                && exception.Date < startDate.AddDays(7))
            .ToListAsync();

        var preferredCandidateSeeds = new List<SlotCandidateSeed>();
        var acceptableCandidateSeeds = new List<SlotCandidateSeed>();

        for (var day = 0; day < 7; day++)
        {
            var currentDayDate = startDate.AddDays(day);
            var currentDayOfWeek = (int)currentDayDate.DayOfWeek;
            var currentDateTime = currentDayDate.ToDateTime(TimeOnly.MinValue);

            var participantWindows = distinctParticipantIds.ToDictionary(
                id => id,
                id => BuildMeetingWindow(settingsByUserId[id].MeetingPreferences, currentDateTime));

            var dayBusyBlocks = BuildDayBusyBlocks(
                distinctParticipantIds,
                allEvents,
                baseScheduleEntries,
                meetings,
                currentDayDate,
                currentDayOfWeek,
                baseScheduleExceptions);

            var acceptableFreeByUser = new List<List<TimeRange>>();
            var preferredFreeByUser = new List<List<TimeRange>>();

            foreach (var userId in distinctParticipantIds)
            {
                var window = participantWindows[userId];
                var hardBusyRanges = dayBusyBlocks
                    .Where(block => block.UserId == userId && block.IsHard)
                    .Select(block => new TimeRange(block.StartTime, block.EndTime))
                    .ToList();

                var acceptableBaseRange = new TimeRange(
                    window.AcceptableStart.ToUniversalTime(),
                    window.AcceptableEnd.ToUniversalTime());
                var preferredBaseRange = new TimeRange(
                    window.PreferredStart.ToUniversalTime(),
                    window.PreferredEnd.ToUniversalTime());

                acceptableFreeByUser.Add(SubtractBusyIntervals(acceptableBaseRange, hardBusyRanges));
                preferredFreeByUser.Add(SubtractBusyIntervals(preferredBaseRange, hardBusyRanges));
            }

            var preferredCommonFree = IntersectAll(preferredFreeByUser);
            var preferredCandidates = BuildSlotCandidates(
                day,
                preferredCommonFree,
                dayBusyBlocks,
                durationMinutes,
                earliestFutureStartUtc,
                true);

            preferredCandidateSeeds.AddRange(preferredCandidates);

            var acceptableCommonFree = IntersectAll(acceptableFreeByUser);
            acceptableCandidateSeeds.AddRange(BuildSlotCandidates(
                day,
                acceptableCommonFree,
                dayBusyBlocks,
                durationMinutes,
                earliestFutureStartUtc,
                false));
        }

        var candidateSeeds = preferredCandidateSeeds.Any()
            ? preferredCandidateSeeds
            : acceptableCandidateSeeds;

        if (!candidateSeeds.Any())
            return (true, string.Empty, new List<TimeSlotDto>());

        var uniqueSlotStarts = candidateSeeds
            .Select(seed => seed.StartTime)
            .Distinct()
            .OrderBy(value => value)
            .ToList();

        var resourceMapsByUser = new Dictionary<Guid, Dictionary<DateTime, int>>();
        foreach (var userId in distinctParticipantIds)
        {
            resourceMapsByUser[userId] = await _userStateService.GetProjectedResourcesAsync(userId, uniqueSlotStarts);
        }

        var result = candidateSeeds
            .Select(seed => ScoreCandidate(seed, distinctParticipantIds, resourceMapsByUser))
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.DayOffset)
            .ThenBy(candidate => candidate.Slot.StartTime)
            .Select(candidate => candidate.Slot)
            .ToList();

        return (true, string.Empty, result);
    }

    private static UserSettings ParseSettings(string? settingsJson)
    {
        if (string.IsNullOrWhiteSpace(settingsJson))
            return new UserSettings();

        try
        {
            return JsonSerializer.Deserialize<UserSettings>(settingsJson, SettingsJsonOptions) ?? new UserSettings();
        }
        catch
        {
            return new UserSettings();
        }
    }

    private static MeetingWindow BuildMeetingWindow(MeetingPreferences preferences, DateTime date)
    {
        var preset = preferences.Preset?.ToLowerInvariant() ?? "day";
        var defaultWindow = preset switch
        {
            "morning" => new MeetingWindow(
                date.Date.AddHours(9),
                date.Date.AddHours(13),
                date.Date.AddHours(8),
                date.Date.AddHours(15)),
            "evening" => new MeetingWindow(
                date.Date.AddHours(16),
                date.Date.AddHours(21),
                date.Date.AddHours(13),
                date.Date.AddHours(22)),
            "any" => new MeetingWindow(
                date.Date.AddHours(9),
                date.Date.AddHours(21),
                date.Date.AddHours(8),
                date.Date.AddHours(22)),
            _ => new MeetingWindow(
                date.Date.AddHours(11),
                date.Date.AddHours(18),
                date.Date.AddHours(10),
                date.Date.AddHours(20))
        };

        var acceptableStart = TryApplyTimeOverride(date, preferences.EarliestTime) ?? defaultWindow.AcceptableStart;
        var acceptableEnd = TryApplyTimeOverride(date, preferences.LatestTime) ?? defaultWindow.AcceptableEnd;

        if (acceptableStart >= acceptableEnd)
            return defaultWindow;

        var preferredStart = defaultWindow.PreferredStart < acceptableStart ? acceptableStart : defaultWindow.PreferredStart;
        var preferredEnd = defaultWindow.PreferredEnd > acceptableEnd ? acceptableEnd : defaultWindow.PreferredEnd;

        if (preferredStart >= preferredEnd)
        {
            preferredStart = acceptableStart;
            preferredEnd = acceptableEnd;
        }

        return new MeetingWindow(preferredStart, preferredEnd, acceptableStart, acceptableEnd);
    }

    private static DateTime? TryApplyTimeOverride(DateTime date, string? timeValue)
    {
        if (string.IsNullOrWhiteSpace(timeValue) || !TimeOnly.TryParse(timeValue, out var time))
            return null;

        return date.Date.Add(time.ToTimeSpan());
    }

    private static List<BusyBlock> BuildDayBusyBlocks(
        IReadOnlyCollection<Guid> participantIds,
        List<Event> allEvents,
        List<BaseScheduleEntry> baseScheduleEntries,
        List<Meeting> meetings,
        DateOnly currentDayDate,
        int currentDayOfWeek,
        List<BaseScheduleOccurrenceException> baseScheduleExceptions)
    {
        var startOfDayUtc = currentDayDate.ToDateTime(TimeOnly.MinValue).ToUniversalTime();
        var endOfDayUtc = currentDayDate.AddDays(1).ToDateTime(TimeOnly.MinValue).ToUniversalTime();
        var dayEndExclusiveUtc = currentDayDate.ToDateTime(new TimeOnly(23, 59)).ToUniversalTime().AddMinutes(1);
        var result = new List<BusyBlock>();

        result.AddRange(allEvents
            .Where(e => e.StartTime < dayEndExclusiveUtc
                        && e.EndTime > startOfDayUtc
                        && participantIds.Contains(e.UserId))
            .Select(e => new BusyBlock(
                e.UserId,
                e.StartTime,
                e.EndTime,
                e.Type == EventType.Mandatory,
                e.Type)));

        result.AddRange(baseScheduleEntries
            .Where(e => e.DayOfWeek == currentDayOfWeek
                        && participantIds.Contains(e.UserId)
                        && e.EffectiveFromDate <= currentDayDate
                        && (e.EffectiveToDate == null || e.EffectiveToDate > currentDayDate)
                        && !baseScheduleExceptions.Any(exception =>
                            exception.BaseScheduleEntryId == e.Id
                            && exception.Date == currentDayDate))
            .Select(e => new BusyBlock(
                e.UserId,
                currentDayDate.ToDateTime(TimeOnly.FromTimeSpan(e.StartTime)).ToUniversalTime(),
                currentDayDate.ToDateTime(TimeOnly.FromTimeSpan(e.EndTime)).ToUniversalTime(),
                e.Type == EventType.Mandatory,
                e.Type)));

        foreach (var meeting in meetings)
        {
            var meetingEndUtc = meeting.StartTime.Add(meeting.Duration);
            if (meeting.StartTime >= endOfDayUtc || meetingEndUtc <= startOfDayUtc)
                continue;

            if (participantIds.Contains(meeting.InitiatorId))
            {
                result.Add(new BusyBlock(
                    meeting.InitiatorId,
                    meeting.StartTime,
                    meetingEndUtc,
                    true,
                    EventType.Mandatory));
            }

            foreach (var participant in meeting.Participants.Where(p => p.Status == ParticipantStatus.Accepted
                                                                        && participantIds.Contains(p.UserId)))
            {
                result.Add(new BusyBlock(
                    participant.UserId,
                    meeting.StartTime,
                    meetingEndUtc,
                    true,
                    EventType.Mandatory));
            }
        }

        return result;
    }

    private static List<SlotCandidateSeed> BuildSlotCandidates(
        int dayOffset,
        IEnumerable<TimeRange> freeRanges,
        IEnumerable<BusyBlock> dayBusyBlocks,
        int durationMinutes,
        DateTime earliestFutureStartUtc,
        bool withinPreferredWindow)
    {
        var candidates = new List<SlotCandidateSeed>();

        foreach (var range in freeRanges)
        {
            var cursor = RoundUpToStep(Max(range.Start, earliestFutureStartUtc), 30);
            while (cursor.AddMinutes(durationMinutes) <= range.End)
            {
                var slotEnd = cursor.AddMinutes(durationMinutes);
                var softConflictCount = dayBusyBlocks.Count(block =>
                    !block.IsHard && block.StartTime < slotEnd && block.EndTime > cursor);

                candidates.Add(new SlotCandidateSeed(
                    dayOffset,
                    cursor,
                    slotEnd,
                    withinPreferredWindow,
                    softConflictCount));

                cursor = cursor.AddMinutes(30);
            }
        }

        return candidates;
    }

    private static List<TimeRange> SubtractBusyIntervals(TimeRange baseRange, IEnumerable<TimeRange> occupiedRanges)
    {
        if (baseRange.Start >= baseRange.End)
            return new List<TimeRange>();

        var occupied = MergeRanges(occupiedRanges
            .Select(range => new TimeRange(Max(baseRange.Start, range.Start), Min(baseRange.End, range.End)))
            .Where(range => range.Start < range.End)
            .OrderBy(range => range.Start)
            .ToList());

        if (!occupied.Any())
            return new List<TimeRange> { baseRange };

        var free = new List<TimeRange>();
        var cursor = baseRange.Start;

        foreach (var range in occupied)
        {
            if (range.Start > cursor)
                free.Add(new TimeRange(cursor, range.Start));

            if (range.End > cursor)
                cursor = range.End;

            if (cursor >= baseRange.End)
                break;
        }

        if (cursor < baseRange.End)
            free.Add(new TimeRange(cursor, baseRange.End));

        return free;
    }

    private static List<TimeRange> IntersectAll(IReadOnlyList<List<TimeRange>> rangeLists)
    {
        if (rangeLists.Count == 0)
            return new List<TimeRange>();

        var current = rangeLists[0];
        for (var index = 1; index < rangeLists.Count; index++)
        {
            current = IntersectRanges(current, rangeLists[index]);
            if (!current.Any())
                break;
        }

        return current;
    }

    private static List<TimeRange> IntersectRanges(IReadOnlyList<TimeRange> left, IReadOnlyList<TimeRange> right)
    {
        var result = new List<TimeRange>();
        var i = 0;
        var j = 0;

        while (i < left.Count && j < right.Count)
        {
            var start = Max(left[i].Start, right[j].Start);
            var end = Min(left[i].End, right[j].End);

            if (start < end)
                result.Add(new TimeRange(start, end));

            if (left[i].End <= right[j].End)
            {
                i++;
            }
            else
            {
                j++;
            }
        }

        return result;
    }

    private static List<TimeRange> MergeRanges(IReadOnlyList<TimeRange> ranges)
    {
        if (ranges.Count == 0)
            return new List<TimeRange>();

        var merged = new List<TimeRange> { ranges[0] };
        for (var index = 1; index < ranges.Count; index++)
        {
            var current = ranges[index];
            var last = merged[^1];

            if (current.Start <= last.End)
            {
                merged[^1] = new TimeRange(last.Start, Max(last.End, current.End));
            }
            else
            {
                merged.Add(current);
            }
        }

        return merged;
    }

    private static SlotCandidate ScoreCandidate(
        SlotCandidateSeed seed,
        IReadOnlyCollection<Guid> participantIds,
        IReadOnlyDictionary<Guid, Dictionary<DateTime, int>> resourceMapsByUser)
    {
        var minResource = participantIds
            .Select(userId => resourceMapsByUser.TryGetValue(userId, out var map) && map.TryGetValue(seed.StartTime, out var resource)
                ? resource
                : 0)
            .Min();

        var lowEnergy = minResource < 30;
        var requiresMoving = seed.SoftConflictCount > 0;
        var score = 100;

        if (!seed.WithinPreferredWindow)
            score -= 25;

        if (requiresMoving)
            score -= 20 + Math.Min(20, Math.Max(0, seed.SoftConflictCount - 1) * 5);

        score -= GetEnergyPenalty(minResource);
        score -= seed.DayOffset * 2;

        var suitability = "Optimal";
        var descriptionParts = new List<string>();

        if (requiresMoving)
        {
            suitability = "RequiresMoving";
            descriptionParts.Add("Потребуется перенос гибких дел");
        }
        else if (lowEnergy)
        {
            suitability = "LowEnergy";
            descriptionParts.Add("К моменту встречи у кого-то из участников будет низкий ресурс");
        }
        else if (!seed.WithinPreferredWindow)
        {
            suitability = "Compromise";
            descriptionParts.Add("Слот найден вне общего предпочтительного окна");
        }

        if (!seed.WithinPreferredWindow && suitability != "Compromise")
            descriptionParts.Add("Время выходит за предпочтительное окно");

        if (!descriptionParts.Any())
            descriptionParts.Add("Свободное и комфортное время для всех");

        return new SlotCandidate(
            seed.DayOffset,
            score,
            new TimeSlotDto
            {
                StartTime = seed.StartTime,
                EndTime = seed.EndTime,
                Suitability = suitability,
                Description = string.Join(". ", descriptionParts)
            });
    }

    private static int GetEnergyPenalty(int minResource) => minResource switch
    {
        < 15 => 35,
        < 30 => 20,
        < 45 => 8,
        _ => 0
    };

    private static DateTime Max(DateTime left, DateTime right) => left >= right ? left : right;

    private static DateTime Min(DateTime left, DateTime right) => left <= right ? left : right;

    private static DateTime RoundUpToStep(DateTime valueUtc, int stepMinutes)
    {
        var value = valueUtc.ToUniversalTime();
        var remainder = value.Minute % stepMinutes;
        var rounded = new DateTime(value.Year, value.Month, value.Day, value.Hour, value.Minute, 0, DateTimeKind.Utc);

        if (value.Second > 0 || value.Millisecond > 0)
            rounded = rounded.AddMinutes(1);

        if (remainder == 0 && value.Second == 0 && value.Millisecond == 0)
            return rounded;

        var minutesToAdd = (stepMinutes - (rounded.Minute % stepMinutes)) % stepMinutes;
        return rounded.AddMinutes(minutesToAdd);
    }

    private sealed class UserSettings
    {
        public MeetingPreferences MeetingPreferences { get; set; } = new();
    }

    private sealed class MeetingPreferences
    {
        public string Preset { get; set; } = "day";
        public string EarliestTime { get; set; } = "10:00";
        public string LatestTime { get; set; } = "20:00";
    }

    private sealed record MeetingWindow(
        DateTime PreferredStart,
        DateTime PreferredEnd,
        DateTime AcceptableStart,
        DateTime AcceptableEnd);

    private sealed record BusyBlock(
        Guid UserId,
        DateTime StartTime,
        DateTime EndTime,
        bool IsHard,
        EventType Type);

    private sealed record TimeRange(
        DateTime Start,
        DateTime End);

    private sealed record SlotCandidateSeed(
        int DayOffset,
        DateTime StartTime,
        DateTime EndTime,
        bool WithinPreferredWindow,
        int SoftConflictCount);

    private sealed record SlotCandidate(
        int DayOffset,
        int Score,
        TimeSlotDto Slot);
}
