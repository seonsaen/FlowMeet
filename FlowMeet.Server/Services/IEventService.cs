using FlowMeet.Server.Models.DTOs;

namespace FlowMeet.Server.Services;

public interface IEventService
{
    Task<List<EventResponse>> GetUserScheduleAsync(Guid userId);
    Task<(bool IsSuccess, string ErrorMessage, Guid? EventId)> CreateEventAsync(Guid userId, CreateEventRequest request);
    Task<(bool IsSuccess, string ErrorMessage)> UpdateEventAsync(Guid userId, Guid eventId, UpdateEventRequest request);
    Task<bool> DeleteEventAsync(Guid userId, Guid id);
}
