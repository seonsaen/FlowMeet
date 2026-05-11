using FlowMeet.Server.Models.DTOs;
using FlowMeet.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowMeet.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PlanningController : AuthorizedApiController
{
    private readonly IPlanningService _planningService;

    public PlanningController(IPlanningService planningService)
    {
        _planningService = planningService;
    }
    
    [HttpPost("find-slots")]
    public async Task<ActionResult<List<TimeSlotDto>>> FindSlots([FromBody] PlanningRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
            return UnauthorizedToken<List<TimeSlotDto>>();
        
        if (request.ParticipantIds == null)
            return ErrorResult<List<TimeSlotDto>>("Список участников обязателен");
        
        if (request.DurationMinutes <= 0)
            return ErrorResult<List<TimeSlotDto>>("Длительность встречи должна быть положительной");
        
        var participantIds = request.ParticipantIds
            .Append(userId)
            .Distinct()
            .ToList();

        var (isSuccess, errorMessage, slots) = await _planningService.FindGroupSlotsAsync(
            userId,
            participantIds, 
            request.StartDate, 
            request.DurationMinutes);

        if (!isSuccess)
            return ErrorResult<List<TimeSlotDto>>(errorMessage);

        return Ok(slots);
    }
}
