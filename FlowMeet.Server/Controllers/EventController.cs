using FlowMeet.Server.Models.DTOs;
using FlowMeet.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowMeet.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class EventController : AuthorizedApiController
{
    private readonly IEventService _eventService;

    public EventController(IEventService eventService)
    {
        _eventService = eventService;
    }
    
    [HttpGet("me")]
    public async Task<ActionResult<List<EventResponse>>> GetUserSchedule()
    {
        if (!TryGetCurrentUserId(out var userId))
            return UnauthorizedToken<List<EventResponse>>();
        
        var response = await _eventService.GetUserScheduleAsync(userId);
        return Ok(response);
    }
    
    [HttpPost]
    public async Task<IActionResult> CreateEvent([FromBody] CreateEventRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
            return UnauthorizedToken();
        
        var (isSuccess, errorMessage, evId) = await _eventService.CreateEventAsync(userId, request);

        if (!isSuccess)
        {
            return ErrorResult(errorMessage);
        }

        return Ok(new { message = "Событие успешно создано", eventId = evId });
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<EventResponse>> UpdateEvent(Guid id, UpdateEventRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
            return UnauthorizedToken<EventResponse>();
        
        var (isSuccess, errorMessage) = await _eventService.UpdateEventAsync(userId, id, request);

        if (!isSuccess)
        {
            return ErrorResult<EventResponse>(errorMessage);
        }
        
        return Ok(new { message = "Событие успешно обновлено", eventId = id });
    }

    [HttpPost("base-occurrence/override")]
    public async Task<IActionResult> OverrideBaseOccurrence([FromBody] OverrideBaseScheduleOccurrenceRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
            return UnauthorizedToken();

        var (isSuccess, errorMessage, eventId) = await _eventService.OverrideBaseOccurrenceAsync(userId, request);
        if (!isSuccess)
            return ErrorResult(errorMessage);

        return Ok(new { message = "Изменение применено только к выбранному дню", eventId });
    }

    [HttpPost("base-occurrence/cancel")]
    public async Task<IActionResult> CancelBaseOccurrence([FromBody] CancelBaseScheduleOccurrenceRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
            return UnauthorizedToken();

        var (isSuccess, errorMessage) = await _eventService.CancelBaseOccurrenceAsync(userId, request);
        if (!isSuccess)
            return ErrorResult(errorMessage);

        return Ok(new { message = "Базовый блок скрыт только для выбранного дня" });
    }

    
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteEvent(Guid id)
    {
        if (!TryGetCurrentUserId(out var userId))
            return UnauthorizedToken();
        
        var isDeleted = await _eventService.DeleteEventAsync(userId, id);

        if (!isDeleted)
        {
            return ErrorResult("Событие не найдено");
        }

        return Ok(new { message = "Событие успешно удалено" });
    }
}
