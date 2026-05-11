using FlowMeet.Server.Data;
using FlowMeet.Server.Models.DTOs;
using FlowMeet.Server.Models.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace FlowMeet.Server.Services;

public class NotificationService : INotificationService
{
    private static readonly JsonSerializerOptions SettingsJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly AppDbContext _context;
    private readonly IEmailService _emailService;

    public NotificationService(AppDbContext context, IEmailService emailService)
    {
        _context = context;
        _emailService = emailService;
    }

    public async Task<List<NotificationDto>> GetNotificationsAsync(Guid userId, bool unreadOnly)
    {
        var now = DateTime.UtcNow;
        var query = _context.Notifications
            .Where(n => n.UserId == userId
                        && ((n.ScheduledFor != null)
                            || (n.DispatchedAt != null && n.DispatchedAt <= now)));

        if (unreadOnly)
            query = query.Where(n => n.ReadAt == null);

        var notifications = await query
            .ToListAsync();

        return notifications
            .OrderBy(notification => notification.ScheduledFor.HasValue ? 0 : 1)
            .ThenBy(notification => notification.ScheduledFor ?? DateTime.MaxValue)
            .ThenByDescending(notification => notification.DispatchedAt ?? notification.CreatedAt)
            .Select(ToDto)
            .ToList();
    }

    public async Task<NotificationDto> CreateNotificationAsync(Guid userId, NotificationType type, string title, string message, Guid? relatedEntityId = null, DateTime? scheduledFor = null)
    {
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = type,
            Title = title,
            Message = message,
            RelatedEntityId = relatedEntityId,
            ScheduledFor = scheduledFor?.ToUniversalTime(),
            CreatedAt = DateTime.UtcNow,
            DispatchedAt = scheduledFor.HasValue ? null : DateTime.UtcNow
        };

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();
        return ToDto(notification);
    }

    public async Task SyncMeetingNotificationsForMeetingAsync(Guid meetingId, CancellationToken cancellationToken = default)
    {
        var meeting = await _context.Meetings
            .Include(currentMeeting => currentMeeting.RelatedGroup)
            .Include(currentMeeting => currentMeeting.Initiator)
            .Include(currentMeeting => currentMeeting.Participants)
            .ThenInclude(participant => participant.User)
            .FirstOrDefaultAsync(currentMeeting => currentMeeting.Id == meetingId, cancellationToken);

        if (meeting == null)
            return;

        var existingScheduledNotifications = await _context.Notifications
            .Where(notification => notification.RelatedEntityId == meetingId
                                   && notification.ScheduledFor != null)
            .ToListAsync(cancellationToken);

        var reminderRecipients = meeting.Status == MeetingStatus.Confirmed && meeting.StartTime > DateTime.UtcNow
            ? BuildScheduledMeetingRecipients(meeting)
            : new List<User>();

        var reminderRecipientIds = reminderRecipients.Select(recipient => recipient.Id).ToHashSet();
        var staleNotifications = existingScheduledNotifications
            .Where(notification => !reminderRecipientIds.Contains(notification.UserId))
            .ToList();

        if (staleNotifications.Count > 0)
            _context.Notifications.RemoveRange(staleNotifications);

        if (reminderRecipients.Count == 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
            return;
        }

        foreach (var recipient in reminderRecipients)
        {
            var leadMinutes = GetMeetingReminderLeadMinutes(recipient.SettingsJson);
            var scheduledFor = meeting.StartTime.AddMinutes(-leadMinutes);
            var counterpart = BuildMeetingCounterpartLabel(meeting, recipient.Id);
            var title = $"Через {leadMinutes} мин встреча «{meeting.Title}»";
            var message = string.IsNullOrWhiteSpace(counterpart)
                ? $"Осталось {leadMinutes} минут до встречи «{meeting.Title}»"
                : $"Осталось {leadMinutes} минут до встречи «{meeting.Title}» с {counterpart}";

            var existing = existingScheduledNotifications.FirstOrDefault(notification => notification.UserId == recipient.Id);
            if (existing == null)
            {
                _context.Notifications.Add(new Notification
                {
                    Id = Guid.NewGuid(),
                    UserId = recipient.Id,
                    Type = NotificationType.Info,
                    Title = title,
                    Message = message,
                    RelatedEntityId = meeting.Id,
                    ScheduledFor = scheduledFor,
                    CreatedAt = DateTime.UtcNow,
                    DispatchedAt = null
                });
            }
            else
            {
                existing.Title = title;
                existing.Message = message;
                existing.ScheduledFor = scheduledFor;
                existing.CreatedAt = DateTime.UtcNow;
                existing.DispatchedAt = null;
                existing.ReadAt = null;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task SyncMeetingNotificationsForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var meetingIds = await _context.Meetings
            .Include(meeting => meeting.Participants)
            .Where(meeting => meeting.Status == MeetingStatus.Confirmed
                              && meeting.StartTime > DateTime.UtcNow
                              && (meeting.InitiatorId == userId
                                  || meeting.Participants.Any(participant => participant.UserId == userId
                                                                             && participant.Status == ParticipantStatus.Accepted)))
            .Select(meeting => meeting.Id)
            .ToListAsync(cancellationToken);

        foreach (var meetingId in meetingIds)
            await SyncMeetingNotificationsForMeetingAsync(meetingId, cancellationToken);
    }

    public async Task<int> DispatchDueScheduledNotificationsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var dueNotifications = await _context.Notifications
            .Include(notification => notification.User)
            .Where(notification => notification.DispatchedAt == null
                                   && notification.ScheduledFor != null
                                   && notification.ScheduledFor <= now)
            .OrderBy(notification => notification.ScheduledFor)
            .Take(100)
            .ToListAsync(cancellationToken);

        if (!dueNotifications.Any())
            return 0;

        foreach (var notification in dueNotifications)
        {
            notification.DispatchedAt = now;

            if (notification.User == null || !ShouldSendScheduledNotificationEmail(notification.User.SettingsJson))
                continue;

            var subject = $"Уведомление FlowMeet: {notification.Title}";
            var textBody = $"{notification.Title}\n\n{notification.Message}\n\nВремя: {notification.ScheduledFor?.ToLocalTime():g}";

            await _emailService.SendAsync(notification.User.Email, subject, textBody, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return dueNotifications.Count;
    }

    public async Task<(bool IsSuccess, string ErrorMessage)> MarkAsReadAsync(Guid userId, Guid notificationId)
    {
        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

        if (notification == null)
            return (false, "Уведомление не найдено");

        notification.ReadAt ??= DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return (true, string.Empty);
    }

    public async Task<(bool IsSuccess, string ErrorMessage)> DeleteNotificationAsync(Guid userId, Guid notificationId)
    {
        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

        if (notification == null)
            return (false, "Уведомление не найдено");

        _context.Notifications.Remove(notification);
        await _context.SaveChangesAsync();
        return (true, string.Empty);
    }

    private static NotificationDto ToDto(Notification notification) => new()
    {
        Id = notification.Id,
        Type = notification.Type,
        Title = notification.Title,
        Message = notification.Message,
        RelatedEntityId = notification.RelatedEntityId,
        ScheduledFor = notification.ScheduledFor,
        CreatedAt = notification.CreatedAt,
        ReadAt = notification.ReadAt
    };

    private static bool ShouldSendScheduledNotificationEmail(string? settingsJson)
    {
        if (string.IsNullOrWhiteSpace(settingsJson))
            return false;

        try
        {
            var settings = JsonSerializer.Deserialize<NotificationSettingsSnapshot>(settingsJson, SettingsJsonOptions);
            return settings?.EmailNotifications == true && settings.Reminders == true;
        }
        catch
        {
            return false;
        }
    }

    private static List<User> BuildScheduledMeetingRecipients(Meeting meeting)
    {
        var recipients = new List<User>();

        if (meeting.Initiator != null)
            recipients.Add(meeting.Initiator);

        recipients.AddRange(meeting.Participants
            .Where(participant => participant.Status == ParticipantStatus.Accepted && participant.User != null)
            .Select(participant => participant.User!));

        return recipients
            .GroupBy(user => user.Id)
            .Select(group => group.First())
            .ToList();
    }

    private static int GetMeetingReminderLeadMinutes(string? settingsJson)
    {
        if (string.IsNullOrWhiteSpace(settingsJson))
            return 60;

        try
        {
            var settings = JsonSerializer.Deserialize<NotificationSettingsSnapshot>(settingsJson, SettingsJsonOptions);
            return Math.Clamp(settings?.MeetingReminderLeadMinutes ?? 60, 5, 1440);
        }
        catch
        {
            return 60;
        }
    }

    private static string BuildMeetingCounterpartLabel(Meeting meeting, Guid userId)
    {
        if (meeting.RelatedGroup != null)
            return $"группой «{meeting.RelatedGroup.Name}»";

        var names = new List<string>();

        if (meeting.InitiatorId != userId && meeting.Initiator != null)
            names.Add($"{meeting.Initiator.FirstName} {meeting.Initiator.LastName}".Trim());

        names.AddRange(meeting.Participants
            .Where(participant => participant.UserId != userId
                                  && participant.Status == ParticipantStatus.Accepted
                                  && participant.User != null)
            .Select(participant => $"{participant.User!.FirstName} {participant.User.LastName}".Trim()));

        return string.Join(", ", names.Where(name => !string.IsNullOrWhiteSpace(name)));
    }

    private sealed class NotificationSettingsSnapshot
    {
        public bool EmailNotifications { get; set; }
        public bool Reminders { get; set; }
        public int MeetingReminderLeadMinutes { get; set; } = 60;
    }
}
