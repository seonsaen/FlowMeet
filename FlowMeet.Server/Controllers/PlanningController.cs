using FlowMeet.Server.Models.DTOs;
using FlowMeet.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace FlowMeet.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PlanningController : ControllerBase
{
    private readonly PlanningService _planningService;

    public PlanningController(PlanningService planningService)
    {
        _planningService = planningService;
    }

    [HttpPost("find-slots")]
    public async Task<ActionResult<List<TimeSlotDto>>> FindSlots([FromBody] PlanningRequest request)
    {
        if (request.ParticipantIds == null || !request.ParticipantIds.Any())
            return BadRequest("Список участников не может быть пустым");

        var slots = await _planningService.FindGroupSlotsAsync(
            request.ParticipantIds, 
            request.StartDate, 
            request.DurationMinutes);

        // Сортируем сначала Идеальные, потом те, что требуют переноса
        var sortedSlots = slots
            .OrderBy(s => s.StartTime) // Сначала ближайшие по времени
            .ThenBy(s => s.Suitability == "Optimal" ? 0 : 1)
            .ToList();

        return Ok(sortedSlots);
    }
}