using System.Security.Claims;
using FlowMeet.Server.Models.DTOs;
using FlowMeet.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowMeet.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        userId = Guid.Empty;

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return !string.IsNullOrWhiteSpace(userIdClaim) && Guid.TryParse(userIdClaim, out userId);
    }

    [HttpGet("me")]
    public async Task<ActionResult<List<NotificationDto>>> GetMyNotifications([FromQuery] bool unreadOnly = false)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized(new { error = "Некорректный токен" });

        var notifications = await _notificationService.GetNotificationsAsync(userId, unreadOnly);
        return Ok(notifications);
    }

    [HttpPost("reminders")]
    public async Task<ActionResult<NotificationDto>> CreateReminder([FromBody] CreateReminderRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized(new { error = "Некорректный токен" });

        var (isSuccess, errorMessage, notification) = await _notificationService.CreateReminderAsync(userId, request);
        if (!isSuccess)
            return BadRequest(new { error = errorMessage });

        return Ok(notification);
    }

    [HttpPut("{id}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized(new { error = "Некорректный токен" });

        var (isSuccess, errorMessage) = await _notificationService.MarkAsReadAsync(userId, id);
        if (!isSuccess)
            return NotFound(new { error = errorMessage });

        return Ok(new { message = "Уведомление прочитано" });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteNotification(Guid id)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized(new { error = "Некорректный токен" });

        var (isSuccess, errorMessage) = await _notificationService.DeleteNotificationAsync(userId, id);
        if (!isSuccess)
            return NotFound(new { error = errorMessage });

        return Ok(new { message = "Уведомление удалено" });
    }
}
