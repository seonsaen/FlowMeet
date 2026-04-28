using System.Security.Claims;
using FlowMeet.Server.Models.DTOs;
using FlowMeet.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowMeet.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PlanningController : ControllerBase
{
    private readonly IPlanningService _planningService;

    public PlanningController(IPlanningService planningService)
    {
        _planningService = planningService;
    }
    
    private bool TryGetCurrentUserId(out Guid userId)
    {
        userId = Guid.Empty;

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return !string.IsNullOrWhiteSpace(userIdClaim) && Guid.TryParse(userIdClaim, out userId);
    }

    [HttpPost("find-slots")]
    public async Task<ActionResult<List<TimeSlotDto>>> FindSlots([FromBody] PlanningRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized(new { error = "Некорректный токен" });
        
        if (request.ParticipantIds == null)
            return BadRequest(new { error = "Список участников обязателен" });
        
        if (request.DurationMinutes <= 0)
            return BadRequest(new { error = "Длительность встречи должна быть положительной" });
        
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
            return BadRequest(new { error = errorMessage });

        return Ok(slots);
    }
}
