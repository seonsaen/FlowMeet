using FlowMeet.Server.Data;
using FlowMeet.Server.Models.DTOs;
using FlowMeet.Server.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace FlowMeet.Server.Services;

public class SocialService : ISocialService
{
    private readonly AppDbContext _context;
    private readonly INotificationService _notificationService;
    private readonly IPlanningService _planningService;

    public SocialService(AppDbContext context, INotificationService notificationService, IPlanningService planningService)
    {
        _context = context;
        _notificationService = notificationService;
        _planningService = planningService;
    }

    public async Task<(bool IsSuccess, string ErrorMessage)> SendFriendRequestAsync(Guid userId, FriendRequest request)
    {
        var targetUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.TargetEmail);
        if (targetUser == null)
            return (false, "Пользователь с таким email не найден");

        if (targetUser.Id == userId)
            return (false, "Нельзя добавить в друзья самого себя");

        var existing = await _context.Friendships
            .FirstOrDefaultAsync(f => 
                (f.RequesterId == userId && f.AddresseeId == targetUser.Id) ||
                (f.RequesterId == targetUser.Id && f.AddresseeId == userId));

        if (existing != null)
        {
            if (existing.Status != FriendshipStatus.Declined)
                return (false, "Заявка уже отправлена или вы уже друзья");

            if (existing.RequesterId == userId && existing.AddresseeId == targetUser.Id)
            {
                existing.Status = FriendshipStatus.Pending;
                await _context.SaveChangesAsync();

                var retryRequester = await _context.Users.FindAsync(userId);
                await _notificationService.CreateNotificationAsync(
                    targetUser.Id,
                    NotificationType.FriendRequest,
                    "Новая заявка в друзья",
                    $"{retryRequester?.FirstName} {retryRequester?.LastName} хочет добавить вас в друзья",
                    userId);

                return (true, string.Empty);
            }

            _context.Friendships.Remove(existing);
            await _context.SaveChangesAsync();
        }

        var friendship = new Friendship
        {
            RequesterId = userId,
            AddresseeId = targetUser.Id,
            Status = FriendshipStatus.Pending 
        };

        _context.Friendships.Add(friendship);
        await _context.SaveChangesAsync();

        var requester = await _context.Users.FindAsync(userId);
        await _notificationService.CreateNotificationAsync(
            targetUser.Id,
            NotificationType.FriendRequest,
            "Новая заявка в друзья",
            $"{requester?.FirstName} {requester?.LastName} хочет добавить вас в друзья",
            userId);

        return (true, string.Empty);
    }

    public async Task<List<AcceptFriendRequest>> GetIncomingFriendRequestsAsync(Guid userId)
    {
        var friendRequests = await _context.Friendships.
            Where(f => f.AddresseeId == userId && f.Status == FriendshipStatus.Pending).ToListAsync();
        var result = new List<AcceptFriendRequest>();
        foreach (var f in friendRequests)
        {
            var requester = await _context.Users.FindAsync(f.RequesterId);
            if (requester != null)
            {
                result.Add(new AcceptFriendRequest
                    {
                        RequesterId = f.RequesterId,
                        RequesterName = requester.FirstName + " " + requester.LastName,
                        RequesterEmail = requester.Email,
                    }
                );
            }
        }
        return result;
    }

    public async Task<(bool IsSuccess, string ErrorMessage)> AcceptRequestAsync(Guid userId, AcceptFriendRequest request)
    {
        var friendship = await _context.Friendships
            .FirstOrDefaultAsync(f => f.RequesterId == request.RequesterId && f.AddresseeId == userId);

        if (friendship == null)
            return (false, "Заявка не найдена");

        if (friendship.Status == FriendshipStatus.Accepted)
            return (false, "Заявка уже принята");
        
        if (friendship.Status == FriendshipStatus.Declined)
            return (false, "Заявка уже отклонена");

        friendship.Status = FriendshipStatus.Accepted;
        await _context.SaveChangesAsync();

        return (true, string.Empty);
    }

    public async Task<(bool IsSuccess, string ErrorMessage)> DeclineRequestAsync(Guid userId,
        AcceptFriendRequest request)
    {
        var friendship = await _context.Friendships
            .FirstOrDefaultAsync(f => f.RequesterId == request.RequesterId && f.AddresseeId == userId);

        if (friendship == null)
            return (false, "Заявка не найдена");

        if (friendship.Status == FriendshipStatus.Accepted)
            return (false, "Заявка уже принята");
        
        if (friendship.Status == FriendshipStatus.Declined)
            return (false, "Заявка уже отклонена");
        
        friendship.Status = FriendshipStatus.Declined;
        await _context.SaveChangesAsync();
        
        return (true, string.Empty);
    }

    public async Task<List<FriendDto>> GetFriendsAsync(Guid userId)
    {
        var nowUtc = DateTime.UtcNow;
        var localNow = DateTime.Now;
        var today = DateOnly.FromDateTime(localNow);
        var currentDayOfWeek = (int)today.DayOfWeek;

        var friendships = await _context.Friendships
            .Include(f => f.Requester)
            .Include(f => f.Addressee)
            .Where(f => (f.RequesterId == userId || f.AddresseeId == userId) 
                        && f.Status == FriendshipStatus.Accepted)
            .ToListAsync();

        var friendIds = friendships
            .Select(friendship => friendship.RequesterId == userId ? friendship.AddresseeId : friendship.RequesterId)
            .Distinct()
            .ToList();

        var busyEventUserIds = await _context.Events
            .Where(ev => friendIds.Contains(ev.UserId)
                         && ev.StartTime <= nowUtc
                         && ev.EndTime > nowUtc)
            .Select(ev => ev.UserId)
            .Distinct()
            .ToListAsync();

        var activeMeetings = await _context.Meetings
            .Include(meeting => meeting.Participants)
            .Where(meeting => meeting.Status == MeetingStatus.Confirmed
                              && meeting.StartTime <= nowUtc
                              && (friendIds.Contains(meeting.InitiatorId)
                                  || meeting.Participants.Any(participant =>
                                      friendIds.Contains(participant.UserId)
                                      && participant.Status == ParticipantStatus.Accepted)))
            .ToListAsync();

        var busyMeetingUserIds = activeMeetings
            .Where(meeting => meeting.StartTime.Add(meeting.Duration) > nowUtc)
            .SelectMany(meeting => meeting.Participants
                .Where(participant => friendIds.Contains(participant.UserId) && participant.Status == ParticipantStatus.Accepted)
                .Select(participant => participant.UserId)
                .Append(meeting.InitiatorId))
            .Distinct()
            .ToList();

        var baseScheduleExceptions = await _context.BaseScheduleOccurrenceExceptions
            .AsNoTracking()
            .Where(exception => friendIds.Contains(exception.UserId) && exception.Date == today)
            .ToListAsync();

        var currentTime = localNow.TimeOfDay;
        var busyBaseUserIds = await _context.BaseScheduleEntries
            .Where(entry => friendIds.Contains(entry.UserId)
                            && entry.DayOfWeek == currentDayOfWeek
                            && entry.EffectiveFromDate <= today
                            && (entry.EffectiveToDate == null || entry.EffectiveToDate > today)
                            && entry.StartTime <= currentTime
                            && entry.EndTime > currentTime)
            .ToListAsync();

        var busyFromBaseIds = busyBaseUserIds
            .Where(entry => !baseScheduleExceptions.Any(exception =>
                exception.BaseScheduleEntryId == entry.Id
                && exception.Date == today))
            .Select(entry => entry.UserId);

        var busyUserIds = busyEventUserIds
            .Concat(busyMeetingUserIds)
            .Concat(busyFromBaseIds)
            .Distinct()
            .ToHashSet();

        var futureMeetings = await _context.Meetings
            .AsNoTracking()
            .Include(meeting => meeting.RelatedGroup)
            .Include(meeting => meeting.Initiator)
            .Include(meeting => meeting.Participants)
            .ThenInclude(participant => participant.User)
            .Where(meeting => meeting.Status == MeetingStatus.Confirmed
                              && meeting.StartTime > nowUtc
                              && (meeting.InitiatorId == userId
                                  || friendIds.Contains(meeting.InitiatorId)
                                  || meeting.Participants.Any(participant => participant.Status == ParticipantStatus.Accepted
                                                                             && (participant.UserId == userId
                                                                                 || friendIds.Contains(participant.UserId)))))
            .OrderBy(meeting => meeting.StartTime)
            .ToListAsync();

        var result = new List<FriendDto>();

        foreach (var f in friendships)
        {
            var friendUser = f.RequesterId == userId ? f.Addressee : f.Requester;
            
            if (friendUser != null)
            {
                var isBusy = busyUserIds.Contains(friendUser.Id);
                var upcomingMeeting = futureMeetings.FirstOrDefault(meeting =>
                    MeetingInvolvesUser(meeting, userId) && MeetingInvolvesUser(meeting, friendUser.Id));

                var targetDurationMinutes = upcomingMeeting == null
                    ? 60
                    : Math.Max(15, (int)Math.Round(upcomingMeeting.Duration.TotalMinutes));

                TimeSlotDto? nearestAvailableSlot = null;
                TimeSlotDto? earlierAvailableSlot = null;

                var planningResult = await _planningService.FindGroupSlotsAsync(
                    userId,
                    [friendUser.Id],
                    today,
                    targetDurationMinutes);

                if (planningResult.IsSuccess)
                {
                    nearestAvailableSlot = planningResult.Slots.FirstOrDefault();
                    if (upcomingMeeting != null)
                    {
                        earlierAvailableSlot = planningResult.Slots.FirstOrDefault(slot => slot.StartTime < upcomingMeeting.StartTime);
                    }
                }

                result.Add(new FriendDto
                {
                    Id = friendUser.Id,
                    FullName = $"{friendUser.FirstName} {friendUser.LastName}",
                    Email = friendUser.Email,
                    Status = isBusy ? "Занят" : "Свободен",
                    IsBusy = isBusy,
                    NearestAvailableSlot = nearestAvailableSlot,
                    UpcomingMeeting = upcomingMeeting == null ? null : ToScheduledMeetingCard(upcomingMeeting),
                    EarlierAvailableSlot = earlierAvailableSlot
                });
            }
        }

        return result;
    }

    public async Task<(bool IsSuccess, string ErrorMessage)> DeleteFriendAsync(Guid userId, Guid friendId)
    {
        var friendship = await _context.Friendships.FirstOrDefaultAsync(f => f.RequesterId == userId && f.AddresseeId == friendId || 
                                                                             f.AddresseeId == userId  && f.RequesterId == friendId);
        if (friendship == null)
        {
            return (false, "Дружба не найдена");
        }
        
        _context.Friendships.Remove(friendship);
        await _context.SaveChangesAsync();
        return (true, string.Empty);
    }

    private static bool MeetingInvolvesUser(Meeting meeting, Guid userId)
    {
        if (meeting.InitiatorId == userId)
            return true;

        return meeting.Participants.Any(participant => participant.UserId == userId && participant.Status == ParticipantStatus.Accepted);
    }

    private static ScheduledMeetingCardDto ToScheduledMeetingCard(Meeting meeting) => new()
    {
        MeetingId = meeting.Id,
        Title = meeting.Title,
        Description = meeting.Description,
        StartTime = meeting.StartTime,
        EndTime = meeting.StartTime.Add(meeting.Duration),
        RelatedGroupId = meeting.RelatedGroupId,
        RelatedGroupName = meeting.RelatedGroup?.Name
    };
}
