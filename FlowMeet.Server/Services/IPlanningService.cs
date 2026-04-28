using FlowMeet.Server.Models.DTOs;

namespace FlowMeet.Server.Services;

public interface IPlanningService
{
    Task<(bool IsSuccess, string ErrorMessage, List<TimeSlotDto> Slots)> FindGroupSlotsAsync(Guid currentUserId, List<Guid> participantIds, DateOnly startDate, int durationMinutes);
}
