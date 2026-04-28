using System.Security.Claims;
using FlowMeet.Server.Models.DTOs;
using FlowMeet.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowMeet.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class GroupController : ControllerBase
{
    private readonly IGroupService _groupService;

    public GroupController(IGroupService groupService)
    {
        _groupService = groupService;
    }
    
    private bool TryGetCurrentUserId(out Guid userId)
    {
        userId = Guid.Empty;

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return !string.IsNullOrWhiteSpace(userIdClaim) && Guid.TryParse(userIdClaim, out userId);
    }
    
    [HttpPost]
    public async Task<ActionResult<GroupResponse>> CreateGroup([FromBody] CreateGroupRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized(new { error = "Некорректный токен" });
        
        var (isSuccess, errorMessage, group) = await _groupService.CreateGroupAsync(userId, request);

        if (!isSuccess)
            return BadRequest(new { error = errorMessage });

        return Ok(group);
    }

    [HttpGet("me")]
    public async Task<ActionResult<List<GroupResponse>>> GetMyGroups()
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized(new { error = "Некорректный токен" });
        
        var result = await _groupService.GetUserGroupsAsync(userId);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(result.Groups);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<GroupResponse>> UpdateGroup(Guid id, [FromBody] UpdateGroupRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized(new { error = "Некорректный токен" });

        var (isSuccess, errorMessage, group) = await _groupService.UpdateGroupAsync(userId, id, request);

        if (!isSuccess)
            return BadRequest(new { error = errorMessage });

        return Ok(group);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteGroup(Guid id)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized(new { error = "Некорректный токен" });

        var (isSuccess, errorMessage) = await _groupService.DeleteGroupAsync(userId, id);

        if (!isSuccess)
            return BadRequest(new { error = errorMessage });

        return Ok(new { message = "Группа удалена" });
    }

    [HttpPost("invite")]
    public async Task<ActionResult<GroupInviteResponse>> InviteToGroup([FromBody] InviteToGroupRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized(new { error = "Некорректный токен" });
        
        var (isSuccess, errorMessage, invite) = await _groupService.InviteToGroupAsync(userId, request);

        if (!isSuccess)
            return BadRequest(new { error = errorMessage });

        return Ok(invite);
    }

    [HttpGet("invites/incoming")]
    public async Task<ActionResult<List<GroupIncomingInviteDto>>> GetIncomingInvites()
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized(new { error = "Некорректный токен" });
        
        var invites = await _groupService.GetIncomingInvitesAsync(userId);
        return Ok(invites);
    }

    [HttpPost("invites/respond")]
    public async Task<IActionResult> RespondToInvite([FromBody] RespondToGroupInviteRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized(new { error = "Некорректный токен" });
        
        var (isSuccess, errorMessage) = await _groupService.RespondToInviteAsync(userId, request);

        if (!isSuccess)
            return BadRequest(new { error = errorMessage });

        return Ok(new
        {
            message = request.IsAccepted ? "Приглашение в группу принято" : "Приглашение в группу отклонено"
        });
    }
}
