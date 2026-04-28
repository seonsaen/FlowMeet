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

        var startUtc = request.StartTime.ToUniversalTime();
        var endUtc = request.EndTime.ToUniversalTime();
        var scheduleChanged = meeting.StartTime != startUtc || meeting.Duration != endUtc - startUtc;
        var oldParticipantIds = meeting.Participants.Select(i => i.UserId).ToHashSet();
        var participantSetChanged = !oldParticipantIds.SetEquals(participantIds);

        meeting.Title = request.Title;
        meeting.Description = request.Description;
        meeting.StartTime = startUtc;
        meeting.Duration = endUtc - startUtc;

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

        if (scheduleChanged || participantSetChanged)
        {
            foreach (var invite in meeting.Participants.Where(i => participantIds.Contains(i.UserId)))
                invite.Status = ParticipantStatus.Pending;

            meeting.Status = MeetingStatus.Proposed;
        }

        await _context.SaveChangesAsync();

        if (scheduleChanged || participantSetChanged)
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

        _context.Meetings.Remove(meeting);
        await _context.SaveChangesAsync();

        return (true, string.Empty);
    }

    public async Task<List<IncomingInviteDto>> GetIncomingInvitesAsync(Guid userId)
    {
        // Ищем приглашения для пользователя
        var invites = await _context.Invites
            .Include(i => i.Meeting)            
            .ThenInclude(m => m!.Initiator)
            .Where(i => i.UserId == userId && i.Status == ParticipantStatus.Pending)
            .OrderBy(i => i.Meeting!.StartTime)
            .ToListAsync();

        return invites.Select(i => new IncomingInviteDto
            {
                MeetingId = i.MeetingId,
                // Берем данные из Initiator
                OrganizerName = $"{i.Meeting!.Initiator!.FirstName} {i.Meeting!.Initiator!.LastName}",
                Title = i.Meeting!.Title,
                Description = i.Meeting!.Description,
                // Возвращаем DateTime напрямую!
                StartTime = i.Meeting!.StartTime 
            })
            .ToList();
    }

    public async Task<(bool IsSuccess, string ErrorMessage)> RespondToInviteAsync(Guid userId, RespondToInviteRequest request)
    {
        var meeting = await _context.Meetings
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
        return (true, string.Empty);
    }
}
