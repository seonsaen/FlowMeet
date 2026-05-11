using FlowMeet.Server.Models.DTOs;
using FlowMeet.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowMeet.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MeetingController : AuthorizedApiController
{
    private readonly IMeetingService _meetingService;

    public MeetingController(IMeetingService meetingService)
    {
        _meetingService = meetingService;
    }

    [HttpPost]
    public async Task<IActionResult> CreateMeeting([FromBody] CreateMeetingRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
            return UnauthorizedToken();
        
        var (isSuccess, errorMessage) = await _meetingService.CreateMeetingAsync(userId, request);

        if (!isSuccess)
            return ErrorResult(errorMessage);

        return Ok(new { message = "Встреча создана, приглашения отправлены" });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateMeeting(Guid id, [FromBody] UpdateMeetingRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
            return UnauthorizedToken();

        var (isSuccess, errorMessage) = await _meetingService.UpdateMeetingAsync(userId, id, request);

        if (!isSuccess)
            return ErrorResult(errorMessage);

        return Ok(new { message = "Встреча обновлена" });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteMeeting(Guid id)
    {
        if (!TryGetCurrentUserId(out var userId))
            return UnauthorizedToken();

        var (isSuccess, errorMessage) = await _meetingService.DeleteMeetingAsync(userId, id);

        if (!isSuccess)
            return ErrorResult(errorMessage);

        return Ok(new { message = "Встреча удалена" });
    }

    [HttpGet("incoming")]
    public async Task<ActionResult<List<IncomingInviteDto>>> GetIncomingInvites()
    {
        if (!TryGetCurrentUserId(out var userId))
            return UnauthorizedToken<List<IncomingInviteDto>>();
        
        var invites = await _meetingService.GetIncomingInvitesAsync(userId);
        return Ok(invites);
    }

    [HttpGet("outgoing")]
    public async Task<ActionResult<List<OutgoingInviteDto>>> GetOutgoingInvites()
    {
        if (!TryGetCurrentUserId(out var userId))
            return UnauthorizedToken<List<OutgoingInviteDto>>();

        var invites = await _meetingService.GetOutgoingInvitesAsync(userId);
        return Ok(invites);
    }

    [HttpGet("mine")]
    public async Task<ActionResult<List<MeetingOverviewDto>>> GetMyMeetings()
    {
        if (!TryGetCurrentUserId(out var userId))
            return UnauthorizedToken<List<MeetingOverviewDto>>();

        var meetings = await _meetingService.GetMyMeetingsAsync(userId);
        return Ok(meetings);
    }

    [HttpGet("history")]
    public async Task<ActionResult<List<MeetingOverviewDto>>> GetMeetingHistory()
    {
        if (!TryGetCurrentUserId(out var userId))
            return UnauthorizedToken<List<MeetingOverviewDto>>();

        var meetings = await _meetingService.GetMeetingHistoryAsync(userId);
        return Ok(meetings);
    }

    [HttpPost("respond")]
    public async Task<IActionResult> RespondToInvite([FromBody] RespondToInviteRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
            return UnauthorizedToken();
        
        var (isSuccess, errorMessage) = await _meetingService.RespondToInviteAsync(userId, request);

        if (!isSuccess)
            return ErrorResult(errorMessage);

        return Ok(new
        {
            message = request.IsAccepted ? "Приглашение принято" : "Приглашение отклонено"
        });
    }
}
