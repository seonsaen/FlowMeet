using FlowMeet.Server.Models.DTOs;
using FlowMeet.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowMeet.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationsController : AuthorizedApiController
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpGet("me")]
    public async Task<ActionResult<List<NotificationDto>>> GetMyNotifications([FromQuery] bool unreadOnly = false)
    {
        if (!TryGetCurrentUserId(out var userId))
            return UnauthorizedToken<List<NotificationDto>>();

        var notifications = await _notificationService.GetNotificationsAsync(userId, unreadOnly);
        return Ok(notifications);
    }

    [HttpPut("{id}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id)
    {
        if (!TryGetCurrentUserId(out var userId))
            return UnauthorizedToken();

        var (isSuccess, errorMessage) = await _notificationService.MarkAsReadAsync(userId, id);
        if (!isSuccess)
            return ErrorResult(errorMessage);

        return Ok(new { message = "Уведомление прочитано" });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteNotification(Guid id)
    {
        if (!TryGetCurrentUserId(out var userId))
            return UnauthorizedToken();

        var (isSuccess, errorMessage) = await _notificationService.DeleteNotificationAsync(userId, id);
        if (!isSuccess)
            return ErrorResult(errorMessage);

        return Ok(new { message = "Уведомление удалено" });
    }
}
