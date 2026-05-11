using FlowMeet.Server.Models.DTOs;
using FlowMeet.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowMeet.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class GroupController : AuthorizedApiController
{
    private readonly IGroupService _groupService;

    public GroupController(IGroupService groupService)
    {
        _groupService = groupService;
    }
    
    [HttpPost]
    public async Task<ActionResult<GroupResponse>> CreateGroup([FromBody] CreateGroupRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
            return UnauthorizedToken<GroupResponse>();
        
        var (isSuccess, errorMessage, group) = await _groupService.CreateGroupAsync(userId, request);

        if (!isSuccess)
            return ErrorResult<GroupResponse>(errorMessage);

        return Ok(group);
    }

    [HttpGet("me")]
    public async Task<ActionResult<List<GroupResponse>>> GetMyGroups()
    {
        if (!TryGetCurrentUserId(out var userId))
            return UnauthorizedToken<List<GroupResponse>>();
        
        var result = await _groupService.GetUserGroupsAsync(userId);

        if (!result.IsSuccess)
            return ErrorResult<List<GroupResponse>>(result.ErrorMessage);

        return Ok(result.Groups);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<GroupResponse>> UpdateGroup(Guid id, [FromBody] UpdateGroupRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
            return UnauthorizedToken<GroupResponse>();

        var (isSuccess, errorMessage, group) = await _groupService.UpdateGroupAsync(userId, id, request);

        if (!isSuccess)
            return ErrorResult<GroupResponse>(errorMessage);

        return Ok(group);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteGroup(Guid id)
    {
        if (!TryGetCurrentUserId(out var userId))
            return UnauthorizedToken();

        var (isSuccess, errorMessage) = await _groupService.DeleteGroupAsync(userId, id);

        if (!isSuccess)
            return ErrorResult(errorMessage);

        return Ok(new { message = "Группа удалена" });
    }

    [HttpPost("invite")]
    public async Task<ActionResult<GroupInviteResponse>> InviteToGroup([FromBody] InviteToGroupRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
            return UnauthorizedToken<GroupInviteResponse>();
        
        var (isSuccess, errorMessage, invite) = await _groupService.InviteToGroupAsync(userId, request);

        if (!isSuccess)
            return ErrorResult<GroupInviteResponse>(errorMessage);

        return Ok(invite);
    }

    [HttpGet("invites/incoming")]
    public async Task<ActionResult<List<GroupIncomingInviteDto>>> GetIncomingInvites()
    {
        if (!TryGetCurrentUserId(out var userId))
            return UnauthorizedToken<List<GroupIncomingInviteDto>>();
        
        var invites = await _groupService.GetIncomingInvitesAsync(userId);
        return Ok(invites);
    }

    [HttpPost("invites/respond")]
    public async Task<IActionResult> RespondToInvite([FromBody] RespondToGroupInviteRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
            return UnauthorizedToken();
        
        var (isSuccess, errorMessage) = await _groupService.RespondToInviteAsync(userId, request);

        if (!isSuccess)
            return ErrorResult(errorMessage);

        return Ok(new
        {
            message = request.IsAccepted ? "Приглашение в группу принято" : "Приглашение в группу отклонено"
        });
    }

    [HttpPut("{id}/members/{memberId}/role")]
    public async Task<ActionResult<GroupResponse>> UpdateMemberRole(Guid id, Guid memberId, [FromBody] UpdateGroupMemberRoleRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
            return UnauthorizedToken<GroupResponse>();

        var (isSuccess, errorMessage, group) = await _groupService.UpdateMemberRoleAsync(userId, id, memberId, request);
        if (!isSuccess)
            return ErrorResult<GroupResponse>(errorMessage);

        return Ok(group);
    }

    [HttpDelete("{id}/members/{memberId}")]
    public async Task<ActionResult<GroupResponse>> RemoveMember(Guid id, Guid memberId)
    {
        if (!TryGetCurrentUserId(out var userId))
            return UnauthorizedToken<GroupResponse>();

        var (isSuccess, errorMessage, group) = await _groupService.RemoveMemberAsync(userId, id, memberId);
        if (!isSuccess)
            return ErrorResult<GroupResponse>(errorMessage);

        return Ok(group);
    }

    [HttpPost("{id}/leave")]
    public async Task<IActionResult> LeaveGroup(Guid id)
    {
        if (!TryGetCurrentUserId(out var userId))
            return UnauthorizedToken();

        var (isSuccess, errorMessage) = await _groupService.LeaveGroupAsync(userId, id);
        if (!isSuccess)
            return ErrorResult(errorMessage);

        return Ok(new { message = "Вы покинули группу" });
    }
}
