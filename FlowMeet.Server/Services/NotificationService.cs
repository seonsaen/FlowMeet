using FlowMeet.Server.Data;
using FlowMeet.Server.Models.DTOs;
using FlowMeet.Server.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace FlowMeet.Server.Services;

public class NotificationService : INotificationService
{
    private readonly AppDbContext _context;

    public NotificationService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<NotificationDto>> GetNotificationsAsync(Guid userId, bool unreadOnly)
    {
        var query = _context.Notifications
            .Where(n => n.UserId == userId);

        if (unreadOnly)
            query = query.Where(n => n.ReadAt == null);

        var notifications = await query
            .OrderBy(n => n.ScheduledFor ?? n.CreatedAt)
            .ThenByDescending(n => n.CreatedAt)
            .ToListAsync();

        return notifications.Select(ToDto).ToList();
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
            CreatedAt = DateTime.UtcNow
        };

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();
        return ToDto(notification);
    }

    public async Task<(bool IsSuccess, string ErrorMessage, NotificationDto? Notification)> CreateReminderAsync(Guid userId, CreateReminderRequest request)
    {
        var userExists = await _context.Users.AnyAsync(u => u.Id == userId);
        if (!userExists)
            return (false, "Пользователь не найден", null);

        if (request.ScheduledFor <= DateTime.UtcNow)
            return (false, "Время напоминания должно быть в будущем", null);

        var notification = await CreateNotificationAsync(
            userId,
            NotificationType.Reminder,
            request.Title,
            request.Message,
            request.RelatedEntityId,
            request.ScheduledFor);

        return (true, string.Empty, notification);
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
}
