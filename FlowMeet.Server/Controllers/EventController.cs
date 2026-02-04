using FlowMeet.Server.Data;
using FlowMeet.Server.Models.Entities;
using FlowMeet.Server.Models.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FlowMeet.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EventController : ControllerBase
{
    private readonly AppDbContext _context;

    public EventController(AppDbContext context)
    {
        _context = context;
    }

    // GET: api/Event/user/{userId}
    // Получить расписание конкретного пользователя
    [HttpGet("user/{userId}")]
    public async Task<ActionResult<List<EventResponse>>> GetUserSchedule(Guid userId)
    {
        var events = await _context.Events
            .Where(e => e.UserId == userId)
            .OrderBy(e => e.StartTime)
            .ToListAsync();
        
        var response = events.Select(e => new EventResponse
        {
            Id = e.Id,
            Title = e.Title,
            Description = e.Description,
            StartTime = e.StartTime,
            EndTime = e.EndTime,
            Type = e.Type.ToString()
        });

        return Ok(response);
    }

    // POST: api/Event
    // Добавить событие
    [HttpPost]
    public async Task<IActionResult> CreateEvent([FromBody] CreateEventRequest request)
    {
        var user = await _context.Users.FindAsync(request.UserId);
        if (user == null)
            return NotFound("Пользователь не найден");

        var newEvent = new Event
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            Title = request.Title,
            Description = request.Description,
            StartTime = request.StartTime.ToUniversalTime(),
            EndTime = request.EndTime.ToUniversalTime(),
            Type = request.Type,
            IsTemplate = false 
        };

        _context.Events.Add(newEvent);
        await _context.SaveChangesAsync();

        return Ok(new { Message = "Событие успешно создано", EventId = newEvent.Id });
    }
    
    // DELETE: api/Event/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteEvent(Guid id)
    {
        var ev = await _context.Events.FindAsync(id);
        if (ev == null) return NotFound();

        _context.Events.Remove(ev);
        await _context.SaveChangesAsync();
        return Ok("Удалено");
    }
}