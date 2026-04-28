using System.Security.Claims;
using FlowMeet.Server.Models.DTOs;
using FlowMeet.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowMeet.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MeetingController : ControllerBase
{
    private readonly IMeetingService _meetingService;

    public MeetingController(IMeetingService meetingService)
    {
        _meetingService = meetingService;
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        userId = Guid.Empty;

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return !string.IsNullOrWhiteSpace(userIdClaim) && Guid.TryParse(userIdClaim, out userId);
    }
    
    [HttpPost]
    public async Task<IActionResult> CreateMeeting([FromBody] CreateMeetingRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized(new { error = "Некорректный токен" });
        
        var (isSuccess, errorMessage) = await _meetingService.CreateMeetingAsync(userId, request);

        if (!isSuccess)
            return BadRequest(new { error = errorMessage });

        return Ok(new { message = "Встреча создана, приглашения отправлены" });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateMeeting(Guid id, [FromBody] UpdateMeetingRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized(new { error = "Некорректный токен" });

        var (isSuccess, errorMessage) = await _meetingService.UpdateMeetingAsync(userId, id, request);

        if (!isSuccess)
            return BadRequest(new { error = errorMessage });

        return Ok(new { message = "Встреча обновлена" });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteMeeting(Guid id)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized(new { error = "Некорректный токен" });

        var (isSuccess, errorMessage) = await _meetingService.DeleteMeetingAsync(userId, id);

        if (!isSuccess)
            return BadRequest(new { error = errorMessage });

        return Ok(new { message = "Встреча удалена" });
    }

    [HttpGet("incoming")]
    public async Task<ActionResult<List<IncomingInviteDto>>> GetIncomingInvites()
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized(new { error = "Некорректный токен" });
        
        var invites = await _meetingService.GetIncomingInvitesAsync(userId);
        return Ok(invites);
    }

    [HttpPost("respond")]
    public async Task<IActionResult> RespondToInvite([FromBody] RespondToInviteRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized(new { error = "Некорректный токен" });
        
        var (isSuccess, errorMessage) = await _meetingService.RespondToInviteAsync(userId, request);

        if (!isSuccess)
            return BadRequest(new { error = errorMessage });

        return Ok(new
        {
            message = request.IsAccepted ? "Приглашение принято" : "Приглашение отклонено"
        });
    }
}
