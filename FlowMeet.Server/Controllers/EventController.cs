using System.Security.Claims;
using FlowMeet.Server.Models.DTOs;
using FlowMeet.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowMeet.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class EventController : ControllerBase
{
    private readonly IEventService _eventService;

    public EventController(IEventService eventService)
    {
        _eventService = eventService;
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        userId = Guid.Empty;

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return !string.IsNullOrWhiteSpace(userIdClaim) && Guid.TryParse(userIdClaim, out userId);
    }
    
    // Получить расписание конкретного пользователя
    [HttpGet("me")]
    public async Task<ActionResult<List<EventResponse>>> GetUserSchedule()
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized(new { error = "Некорректный токен" });
        
        var response = await _eventService.GetUserScheduleAsync(userId);
        return Ok(response);
    }
    
    // Добавить событие
    [HttpPost]
    public async Task<IActionResult> CreateEvent([FromBody] CreateEventRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized(new { error = "Некорректный токен" });
        
        var (isSuccess, errorMessage, evId) = await _eventService.CreateEventAsync(userId, request);

        if (!isSuccess)
        {
            // Возвращаем 400 Bad Request, если пользователь не найден или время указано неверно
            return BadRequest(new { error = errorMessage });
        }

        return Ok(new { message = "Событие успешно создано", eventId = evId });
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<EventResponse>> UpdateEvent(Guid id, UpdateEventRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized(new { error = "Некорректный токен" });
        
        var (isSuccess, errorMessage) = await _eventService.UpdateEventAsync(userId, id, request);

        if (!isSuccess)
        {
            return BadRequest(new { error = errorMessage });
        }
        
        return Ok(new { message = "Событие успешно обновлено", eventId = id });
    }

    
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteEvent(Guid id)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized(new { error = "Некорректный токен" });
        
        var isDeleted = await _eventService.DeleteEventAsync(userId, id);

        if (!isDeleted)
        {
            return NotFound(new { error = "Событие не найдено" });
        }

        return Ok(new { message = "Событие успешно удалено" });
    }
}