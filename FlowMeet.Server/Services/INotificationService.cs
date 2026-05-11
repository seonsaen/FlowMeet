using FlowMeet.Server.Models.DTOs;
using FlowMeet.Server.Models.Entities;

namespace FlowMeet.Server.Services;

public interface INotificationService
{
    Task<List<NotificationDto>> GetNotificationsAsync(Guid userId, bool unreadOnly);
    Task<NotificationDto> CreateNotificationAsync(Guid userId, NotificationType type, string title, string message, Guid? relatedEntityId = null, DateTime? scheduledFor = null);
    Task SyncMeetingNotificationsForMeetingAsync(Guid meetingId, CancellationToken cancellationToken = default);
    Task SyncMeetingNotificationsForUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<int> DispatchDueScheduledNotificationsAsync(CancellationToken cancellationToken = default);
    Task<(bool IsSuccess, string ErrorMessage)> MarkAsReadAsync(Guid userId, Guid notificationId);
    Task<(bool IsSuccess, string ErrorMessage)> DeleteNotificationAsync(Guid userId, Guid notificationId);
}
