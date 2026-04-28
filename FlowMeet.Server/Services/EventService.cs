using FlowMeet.Server.Data;
using FlowMeet.Server.Models.Entities;
using FlowMeet.Server.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace FlowMeet.Server.Services;

public class EventService : IEventService
{
    private readonly AppDbContext _context;

    public EventService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<EventResponse>> GetUserScheduleAsync(Guid userId)
    {
        var events = await _context.Events
            .Where(e => e.UserId == userId)
            .OrderBy(e => e.StartTime)
            .ToListAsync();
        
        return events.Select(e => new EventResponse
        {
            Id = e.Id,
            Title = e.Title,
            Description = e.Description,
            StartTime = e.StartTime,
            EndTime = e.EndTime,
            Type = e.Type.ToString()
        }).ToList();
    }

    public async Task<(bool IsSuccess, string ErrorMessage, Guid? EventId)> CreateEventAsync(Guid userId, CreateEventRequest request)
    {
        // Существует ли пользователь
        var userExists = await _context.Users.AnyAsync(u => u.Id == userId);
        if (!userExists)
        {
            return (false, "Пользователь не найден", null);
        }

        // Проверка времени
        if (request.EndTime <= request.StartTime)
        {
            return (false, "Время окончания должно быть позже времени начала", null);
        }

        // Создание события
        var newEvent = new Event
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = request.Title,
            Description = request.Description,
            StartTime = request.StartTime.ToUniversalTime(),
            EndTime = request.EndTime.ToUniversalTime(),
            Type = request.Type
        };

        _context.Events.Add(newEvent);
        await _context.SaveChangesAsync();

        return (true, string.Empty, newEvent.Id);
    }

    public async Task<(bool IsSuccess, string ErrorMessage)> UpdateEventAsync(Guid userId, Guid eventId,
        UpdateEventRequest request)
    {
        var userExists = await _context.Users.AnyAsync(u => u.Id == userId);
        if (!userExists)
        {
            return (false, "Пользователь не найден");
        }
        
        var ev = await _context.Events.FirstOrDefaultAsync(e => e.Id == eventId && e.UserId == userId);
        if (ev == null)
            return (false, "Событие не найдено");

        // Проверка времени
        if (request.EndTime <= request.StartTime)
        {
            return (false, "Время окончания должно быть позже времени начала");
        }
        
        ev.Title = request.Title;
        ev.Description = request.Description;
        ev.StartTime = request.StartTime.ToUniversalTime();
        ev.EndTime = request.EndTime.ToUniversalTime();
        ev.Type = request.Type;
        
        await _context.SaveChangesAsync();
        
        return (true, string.Empty);
    }

    public async Task<bool> DeleteEventAsync(Guid userId, Guid id)
    {
        var ev = await _context.Events
            .FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId);

        if (ev == null) 
            return false;

        _context.Events.Remove(ev);
        await _context.SaveChangesAsync();
        
        return true;
    }
}
