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
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .OrderBy(e => e.StartTime)
            .ToListAsync();

        var meetings = await _context.Meetings
            .AsNoTracking()
            .Include(m => m.Participants)
            .Where(m => m.Status == MeetingStatus.Confirmed
                        && (m.InitiatorId == userId
                            || m.Participants.Any(p => p.UserId == userId && p.Status == ParticipantStatus.Accepted)))
            .ToListAsync();

        var personalEvents = events.Select(e => new EventResponse
        {
            Id = e.Id,
            Title = e.Title,
            Description = e.Description,
            StartTime = e.StartTime,
            EndTime = e.EndTime,
            Type = e.Type.ToString(),
            Source = "event",
            IsEditable = true
        });

        var acceptedMeetings = meetings.Select(m => new EventResponse
        {
            Id = m.Id,
            Title = m.Title,
            Description = m.Description,
            StartTime = m.StartTime,
            EndTime = m.StartTime.Add(m.Duration),
            Type = EventType.Mandatory.ToString(),
            Source = "meeting",
            IsEditable = false
        });
        
        return personalEvents
            .Concat(acceptedMeetings)
            .OrderBy(e => e.StartTime)
            .ToList();
    }

    public async Task<(bool IsSuccess, string ErrorMessage, Guid? EventId)> CreateEventAsync(Guid userId, CreateEventRequest request)
    {
        var userExists = await _context.Users.AnyAsync(u => u.Id == userId);
        if (!userExists)
        {
            return (false, "Пользователь не найден", null);
        }
        
        if (request.EndTime <= request.StartTime)
        {
            return (false, "Время окончания должно быть позже времени начала", null);
        }
        
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

    public async Task<(bool IsSuccess, string ErrorMessage, Guid? EventId)> OverrideBaseOccurrenceAsync(Guid userId, OverrideBaseScheduleOccurrenceRequest request)
    {
        var userExists = await _context.Users.AnyAsync(u => u.Id == userId);
        if (!userExists)
            return (false, "Пользователь не найден", null);

        if (request.EndTime <= request.StartTime)
            return (false, "Время окончания должно быть позже времени начала", null);

        var baseEntry = await _context.BaseScheduleEntries
            .FirstOrDefaultAsync(entry => entry.Id == request.BaseScheduleEntryId && entry.UserId == userId);

        if (baseEntry == null)
            return (false, "Базовый блок не найден", null);

        var occurrenceException = await _context.BaseScheduleOccurrenceExceptions
            .FirstOrDefaultAsync(exception =>
                exception.UserId == userId
                && exception.BaseScheduleEntryId == request.BaseScheduleEntryId
                && exception.Date == request.OccurrenceDate);

        Event occurrenceEvent;

        if (occurrenceException?.OverrideEventId is Guid overrideEventId)
        {
            occurrenceEvent = await _context.Events
                .FirstOrDefaultAsync(ev => ev.Id == overrideEventId && ev.UserId == userId)
                ?? new Event
                {
                    Id = Guid.NewGuid(),
                    UserId = userId
                };

            if (occurrenceEvent.Id != overrideEventId)
                _context.Events.Add(occurrenceEvent);
        }
        else
        {
            occurrenceEvent = new Event
            {
                Id = Guid.NewGuid(),
                UserId = userId
            };
            _context.Events.Add(occurrenceEvent);
        }

        occurrenceEvent.Title = request.Title;
        occurrenceEvent.Description = request.Description;
        occurrenceEvent.StartTime = request.StartTime.ToUniversalTime();
        occurrenceEvent.EndTime = request.EndTime.ToUniversalTime();
        occurrenceEvent.Type = request.Type;

        if (occurrenceException == null)
        {
            occurrenceException = new BaseScheduleOccurrenceException
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                BaseScheduleEntryId = request.BaseScheduleEntryId,
                Date = request.OccurrenceDate
            };

            _context.BaseScheduleOccurrenceExceptions.Add(occurrenceException);
        }

        occurrenceException.OverrideEventId = occurrenceEvent.Id;
        await _context.SaveChangesAsync();

        return (true, string.Empty, occurrenceEvent.Id);
    }

    public async Task<(bool IsSuccess, string ErrorMessage)> CancelBaseOccurrenceAsync(Guid userId, CancelBaseScheduleOccurrenceRequest request)
    {
        var userExists = await _context.Users.AnyAsync(u => u.Id == userId);
        if (!userExists)
            return (false, "Пользователь не найден");

        var baseEntry = await _context.BaseScheduleEntries
            .FirstOrDefaultAsync(entry => entry.Id == request.BaseScheduleEntryId && entry.UserId == userId);

        if (baseEntry == null)
            return (false, "Базовый блок не найден");

        var occurrenceException = await _context.BaseScheduleOccurrenceExceptions
            .FirstOrDefaultAsync(exception =>
                exception.UserId == userId
                && exception.BaseScheduleEntryId == request.BaseScheduleEntryId
                && exception.Date == request.OccurrenceDate);

        if (occurrenceException == null)
        {
            occurrenceException = new BaseScheduleOccurrenceException
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                BaseScheduleEntryId = request.BaseScheduleEntryId,
                Date = request.OccurrenceDate,
                OverrideEventId = null
            };

            _context.BaseScheduleOccurrenceExceptions.Add(occurrenceException);
        }

        if (occurrenceException.OverrideEventId is Guid overrideEventId)
        {
            var overrideEvent = await _context.Events
                .FirstOrDefaultAsync(ev => ev.Id == overrideEventId && ev.UserId == userId);

            if (overrideEvent != null)
                _context.Events.Remove(overrideEvent);

            occurrenceException.OverrideEventId = null;
        }

        await _context.SaveChangesAsync();
        return (true, string.Empty);
    }

    public async Task<bool> DeleteEventAsync(Guid userId, Guid id)
    {
        var ev = await _context.Events
            .FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId);

        if (ev == null) 
            return false;

        var relatedOccurrenceException = await _context.BaseScheduleOccurrenceExceptions
            .FirstOrDefaultAsync(exception => exception.UserId == userId && exception.OverrideEventId == id);

        if (relatedOccurrenceException != null)
            relatedOccurrenceException.OverrideEventId = null;

        _context.Events.Remove(ev);
        await _context.SaveChangesAsync();
        
        return true;
    }
}
