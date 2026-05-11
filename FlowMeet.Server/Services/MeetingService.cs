using FlowMeet.Server.Data;
using FlowMeet.Server.Models.DTOs;
using FlowMeet.Server.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace FlowMeet.Server.Services;

public class MeetingService : IMeetingService
{
    private readonly AppDbContext _context;
    private readonly INotificationService _notificationService;

    public MeetingService(AppDbContext context, INotificationService notificationService)
    {
        _context = context;
        _notificationService = notificationService;
    }
    
    public async Task<(bool IsSuccess, string ErrorMessage)> CreateMeetingAsync(Guid userId, CreateMeetingRequest request)
    {
        var organizerExists = await _context.Users.AnyAsync(u => u.Id == userId);
        if (!organizerExists)
            return (false, "Организатор не найден");

        if (request.EndTime <= request.StartTime)
            return (false, "Время окончания должно быть позже времени начала");

        var participantIds = request.ParticipantIds
            .Where(id => id != userId)
            .Distinct()
            .ToList();

        if (!participantIds.Any())
            return (false, "Нужно указать хотя бы одного приглашенного, кроме организатора");

        var existingParticipantIds = await _context.Users
            .Where(u => participantIds.Contains(u.Id))
            .Select(u => u.Id)
            .ToListAsync();

        var missingParticipantIds = participantIds.Except(existingParticipantIds).ToList();
        if (missingParticipantIds.Any())
            return (false, "Один или несколько участников не найдены");

        if (request.GroupId.HasValue)
        {
            var groupValidation = await ValidateGroupMeetingAsync(userId, request.GroupId.Value, participantIds);
            if (!groupValidation.IsSuccess)
                return (false, groupValidation.ErrorMessage);
        }

        var startUtc = request.StartTime.ToUniversalTime();
        var endUtc = request.EndTime.ToUniversalTime();

        var meeting = new Meeting
        {
            Id = Guid.NewGuid(),
            InitiatorId = userId,
            Title = request.Title,
            Description = request.Description,
            StartTime = startUtc,
            Duration = endUtc - startUtc,
            RelatedGroupId = request.GroupId,
            Status = MeetingStatus.Proposed
        };

        _context.Meetings.Add(meeting);

        foreach (var participantId in participantIds)
        {
            _context.Invites.Add(new MeetingInvite
            {
                MeetingId = meeting.Id,
                UserId = participantId,
                Status = ParticipantStatus.Pending
            });
        }

        await _context.SaveChangesAsync();

        foreach (var participantId in participantIds)
        {
            await _notificationService.CreateNotificationAsync(
                participantId,
                NotificationType.MeetingInvite,
                "Новое приглашение на встречу",
                $"Вас пригласили на встречу «{meeting.Title}»",
                meeting.Id,
                meeting.StartTime);
        }

        return (true, string.Empty);
    }

    public async Task<(bool IsSuccess, string ErrorMessage)> UpdateMeetingAsync(Guid userId, Guid meetingId, UpdateMeetingRequest request)
    {
        var meeting = await _context.Meetings
            .Include(m => m.Participants)
            .FirstOrDefaultAsync(m => m.Id == meetingId);

        if (meeting == null)
            return (false, "Встреча не найдена");

        if (meeting.InitiatorId != userId)
            return (false, "Редактировать встречу может только организатор");

        if (request.EndTime <= request.StartTime)
            return (false, "Время окончания должно быть позже времени начала");

        var participantIds = request.ParticipantIds
            .Where(id => id != userId)
            .Distinct()
            .ToList();

        if (!participantIds.Any())
            return (false, "Нужно указать хотя бы одного приглашенного, кроме организатора");

        var existingParticipantIds = await _context.Users
            .Where(u => participantIds.Contains(u.Id))
            .Select(u => u.Id)
            .ToListAsync();

        if (participantIds.Except(existingParticipantIds).Any())
            return (false, "Один или несколько участников не найдены");

        if (request.GroupId.HasValue)
        {
            var groupValidation = await ValidateGroupMeetingAsync(userId, request.GroupId.Value, participantIds);
            if (!groupValidation.IsSuccess)
                return (false, groupValidation.ErrorMessage);
        }

        var startUtc = request.StartTime.ToUniversalTime();
        var endUtc = request.EndTime.ToUniversalTime();
        var scheduleChanged = meeting.StartTime != startUtc || meeting.Duration != endUtc - startUtc;
        var oldParticipantIds = meeting.Participants.Select(i => i.UserId).ToHashSet();
        var participantSetChanged = !oldParticipantIds.SetEquals(participantIds);
        var groupChanged = meeting.RelatedGroupId != request.GroupId;

        meeting.Title = request.Title;
        meeting.Description = request.Description;
        meeting.StartTime = startUtc;
        meeting.Duration = endUtc - startUtc;
        meeting.RelatedGroupId = request.GroupId;

        var removedInvites = meeting.Participants
            .Where(i => !participantIds.Contains(i.UserId))
            .ToList();
        _context.Invites.RemoveRange(removedInvites);

        var currentInviteUserIds = meeting.Participants
            .Except(removedInvites)
            .Select(i => i.UserId)
            .ToHashSet();

        foreach (var participantId in participantIds.Where(id => !currentInviteUserIds.Contains(id)))
        {
            _context.Invites.Add(new MeetingInvite
            {
                MeetingId = meeting.Id,
                UserId = participantId,
                Status = ParticipantStatus.Pending
            });
        }

        if (scheduleChanged || participantSetChanged || groupChanged)
        {
            foreach (var invite in meeting.Participants.Where(i => participantIds.Contains(i.UserId)))
                invite.Status = ParticipantStatus.Pending;

            meeting.Status = MeetingStatus.Proposed;
        }

        await _context.SaveChangesAsync();
        await _notificationService.SyncMeetingNotificationsForMeetingAsync(meeting.Id);

        if (scheduleChanged || participantSetChanged || groupChanged)
        {
            foreach (var participantId in participantIds)
            {
                await _notificationService.CreateNotificationAsync(
                    participantId,
                    NotificationType.MeetingInvite,
                    "Встреча обновлена",
                    $"Проверьте обновленное приглашение «{meeting.Title}»",
                    meeting.Id,
                    meeting.StartTime);
            }
        }

        return (true, string.Empty);
    }

    public async Task<(bool IsSuccess, string ErrorMessage)> DeleteMeetingAsync(Guid userId, Guid meetingId)
    {
        var meeting = await _context.Meetings
            .Include(m => m.Participants)
            .FirstOrDefaultAsync(m => m.Id == meetingId);

        if (meeting == null)
            return (false, "Встреча не найдена");

        if (meeting.InitiatorId != userId)
            return (false, "Удалить встречу может только организатор");

        var relatedNotifications = await _context.Notifications
            .Where(notification => notification.RelatedEntityId == meetingId)
            .ToListAsync();

        if (relatedNotifications.Count > 0)
            _context.Notifications.RemoveRange(relatedNotifications);

        _context.Meetings.Remove(meeting);
        await _context.SaveChangesAsync();

        return (true, string.Empty);
    }

    public async Task<List<IncomingInviteDto>> GetIncomingInvitesAsync(Guid userId)
    {
        await ExpirePastDueInvitesAsync();
        
        var invites = await _context.Invites
            .Include(i => i.Meeting)            
            .ThenInclude(m => m!.Initiator)
            .Where(i => i.UserId == userId && i.Status == ParticipantStatus.Pending)
            .OrderBy(i => i.Meeting!.StartTime)
            .ToListAsync();

        return invites.Select(i => new IncomingInviteDto
            {
                MeetingId = i.MeetingId,
                OrganizerName = $"{i.Meeting!.Initiator!.FirstName} {i.Meeting!.Initiator!.LastName}",
                Title = i.Meeting!.Title,
                Description = i.Meeting!.Description,
                StartTime = i.Meeting!.StartTime,
                EndTime = i.Meeting!.StartTime.Add(i.Meeting!.Duration)
            })
            .ToList();
    }

    public async Task<List<OutgoingInviteDto>> GetOutgoingInvitesAsync(Guid userId)
    {
        await ExpirePastDueInvitesAsync();

        var meetings = await _context.Meetings
            .Include(meeting => meeting.Participants)
            .ThenInclude(participant => participant.User)
            .Where(meeting => meeting.InitiatorId == userId && meeting.Status == MeetingStatus.Proposed)
            .OrderBy(meeting => meeting.StartTime)
            .ToListAsync();

        return meetings.Select(meeting =>
        {
            var pendingParticipants = meeting.Participants
                .Where(participant => participant.Status == ParticipantStatus.Pending)
                .Select(participant => participant.User is null
                    ? "Участник"
                    : $"{participant.User.FirstName} {participant.User.LastName}".Trim())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToList();

            return new OutgoingInviteDto
            {
                MeetingId = meeting.Id,
                Title = meeting.Title,
                Description = meeting.Description,
                StartTime = meeting.StartTime,
                EndTime = meeting.StartTime.Add(meeting.Duration),
                PendingParticipantsCount = meeting.Participants.Count(participant => participant.Status == ParticipantStatus.Pending),
                AcceptedParticipantsCount = meeting.Participants.Count(participant => participant.Status == ParticipantStatus.Accepted),
                TotalParticipantsCount = meeting.Participants.Count,
                PendingParticipantNames = pendingParticipants
            };
        }).ToList();
    }

    public async Task<List<MeetingOverviewDto>> GetMyMeetingsAsync(Guid userId)
    {
        await ExpirePastDueInvitesAsync();

        var meetings = await _context.Meetings
            .AsNoTracking()
            .Include(meeting => meeting.Initiator)
            .Include(meeting => meeting.RelatedGroup)
            .Include(meeting => meeting.Participants)
            .ThenInclude(participant => participant.User)
            .Where(meeting => meeting.Status != MeetingStatus.Cancelled
                              && meeting.StartTime >= DateTime.UtcNow
                              && (meeting.InitiatorId == userId
                                  || meeting.Participants.Any(participant => participant.UserId == userId
                                                                             && participant.Status == ParticipantStatus.Accepted)))
            .OrderBy(meeting => meeting.StartTime)
            .ToListAsync();

        return meetings.Select(meeting => ToMeetingOverviewDto(meeting, userId, canEdit: meeting.InitiatorId == userId)).ToList();
    }

    public async Task<List<MeetingOverviewDto>> GetMeetingHistoryAsync(Guid userId)
    {
        await ExpirePastDueInvitesAsync();

        var now = DateTime.UtcNow;
        var meetings = await _context.Meetings
            .AsNoTracking()
            .Include(meeting => meeting.Initiator)
            .Include(meeting => meeting.RelatedGroup)
            .Include(meeting => meeting.Participants)
            .ThenInclude(participant => participant.User)
            .Where(meeting => meeting.Status == MeetingStatus.Confirmed
                              && meeting.StartTime < now
                              && (meeting.InitiatorId == userId
                                  || meeting.Participants.Any(participant => participant.UserId == userId
                                                                             && participant.Status == ParticipantStatus.Accepted)))
            .OrderByDescending(meeting => meeting.StartTime)
            .Take(50)
            .ToListAsync();

        return meetings
            .Where(meeting => meeting.StartTime.Add(meeting.Duration) <= now)
            .Select(meeting => ToMeetingOverviewDto(meeting, userId, canEdit: false))
            .ToList();
    }

    public async Task<(bool IsSuccess, string ErrorMessage)> RespondToInviteAsync(Guid userId, RespondToInviteRequest request)
    {
        var meeting = await _context.Meetings
            .Include(currentMeeting => currentMeeting.RelatedGroup)
            .Include(currentMeeting => currentMeeting.Initiator)
            .FirstOrDefaultAsync(m => m.Id == request.MeetingId);

        if (meeting == null)
            return (false, "Встреча не найдена");

        var invites = await _context.Invites
            .Where(i => i.MeetingId == request.MeetingId)
            .ToListAsync();

        var invite = invites.FirstOrDefault(i => i.UserId == userId);
        if (invite == null)
            return (false, "Приглашение не найдено");

        if (invite.Status != ParticipantStatus.Pending)
            return (false, "Вы уже дали ответ на это приглашение");

        invite.Status = request.IsAccepted ? ParticipantStatus.Accepted : ParticipantStatus.Declined;

        if (invites.Any(i => i.Status == ParticipantStatus.Declined))
        {
            meeting.Status = MeetingStatus.Cancelled;
        }
        else if (invites.All(i => i.Status == ParticipantStatus.Accepted))
        {
            meeting.Status = MeetingStatus.Confirmed;
        }
        else
        {
            meeting.Status = MeetingStatus.Proposed;
        }

        await _context.SaveChangesAsync();
        await _notificationService.SyncMeetingNotificationsForMeetingAsync(meeting.Id);

        var actorName = await _context.Users
            .Where(user => user.Id == userId)
            .Select(user => $"{user.FirstName} {user.LastName}".Trim())
            .FirstOrDefaultAsync() ?? "Участник";

        await _notificationService.CreateNotificationAsync(
            meeting.InitiatorId,
            NotificationType.Info,
            request.IsAccepted ? "Участник подтвердил встречу" : "Участник отклонил встречу",
            request.IsAccepted
                ? $"{actorName} подтвердил встречу «{meeting.Title}»"
                : $"{actorName} отклонил встречу «{meeting.Title}»",
            meeting.Id);

        return (true, string.Empty);
    }

    private async Task ExpirePastDueInvitesAsync()
    {
        var now = DateTime.UtcNow;
        var meetings = await _context.Meetings
            .Include(meeting => meeting.Participants)
            .Where(meeting => meeting.Status == MeetingStatus.Proposed
                              && meeting.StartTime <= now
                              && meeting.Participants.Any(participant => participant.Status == ParticipantStatus.Pending))
            .ToListAsync();

        if (!meetings.Any())
            return;

        foreach (var meeting in meetings)
        {
            var expiredParticipants = meeting.Participants
                .Where(participant => participant.Status == ParticipantStatus.Pending)
                .ToList();

            if (!expiredParticipants.Any())
                continue;

            foreach (var participant in expiredParticipants)
                participant.Status = ParticipantStatus.Declined;

            meeting.Status = MeetingStatus.Cancelled;

            await _notificationService.CreateNotificationAsync(
                meeting.InitiatorId,
                NotificationType.Info,
                "Встреча отменена автоматически",
                $"Встреча «{meeting.Title}» была отменена, потому что приглашенные не успели ответить вовремя",
                meeting.Id);

            foreach (var participant in expiredParticipants)
            {
                await _notificationService.CreateNotificationAsync(
                    participant.UserId,
                    NotificationType.Info,
                    "Приглашение на встречу истекло",
                    $"Приглашение на встречу «{meeting.Title}» автоматически отклонено, потому что время встречи уже прошло",
                    meeting.Id);
            }
        }

        await _context.SaveChangesAsync();
    }

    private async Task<(bool IsSuccess, string ErrorMessage)> ValidateGroupMeetingAsync(Guid userId, Guid groupId, List<Guid> participantIds)
    {
        var group = await _context.Groups
            .Include(currentGroup => currentGroup.Members)
            .FirstOrDefaultAsync(currentGroup => currentGroup.Id == groupId);

        if (group == null)
            return (false, "Группа не найдена");

        if (group.Members.All(member => member.UserId != userId))
            return (false, "Вы не состоите в этой группе");

        var groupMemberIds = group.Members.Select(member => member.UserId).ToHashSet();
        if (participantIds.Any(participantId => !groupMemberIds.Contains(participantId)))
            return (false, "Для групповой встречи можно выбрать только участников этой группы");

        return (true, string.Empty);
    }

    private static List<Guid> BuildParticipantIds(Meeting meeting)
    {
        return meeting.Participants
            .Select(participant => participant.UserId)
            .Prepend(meeting.InitiatorId)
            .Distinct()
            .ToList();
    }

    private static List<string> BuildParticipantNames(Meeting meeting)
    {
        var names = new List<string>();

        if (meeting.Initiator != null)
        {
            var organizerName = $"{meeting.Initiator.FirstName} {meeting.Initiator.LastName}".Trim();
            if (!string.IsNullOrWhiteSpace(organizerName))
                names.Add(organizerName);
        }
        else
        {
            names.Add("Организатор");
        }

        names.AddRange(meeting.Participants
            .Select(participant => participant.User == null
                ? "Участник"
                : $"{participant.User.FirstName} {participant.User.LastName}".Trim())
            .Where(name => !string.IsNullOrWhiteSpace(name)));

        return names
            .Distinct()
            .ToList();
    }

    private static MeetingOverviewDto ToMeetingOverviewDto(Meeting meeting, Guid userId, bool canEdit) => new()
    {
        MeetingId = meeting.Id,
        Title = meeting.Title,
        Description = meeting.Description,
        StartTime = meeting.StartTime,
        EndTime = meeting.StartTime.Add(meeting.Duration),
        Status = meeting.Status,
        OrganizerId = meeting.InitiatorId,
        OrganizerName = meeting.Initiator == null
            ? "Организатор"
            : $"{meeting.Initiator.FirstName} {meeting.Initiator.LastName}".Trim(),
        RelatedGroupId = meeting.RelatedGroupId,
        RelatedGroupName = meeting.RelatedGroup?.Name,
        CanEdit = canEdit,
        ParticipantIds = BuildParticipantIds(meeting),
        ParticipantNames = BuildParticipantNames(meeting)
    };
}
