using FlowMeet.Server.Models.DTOs;
using FlowMeet.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowMeet.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SocialController : AuthorizedApiController
{
    private readonly ISocialService _socialService;

    public SocialController(ISocialService socialService)
    {
        _socialService = socialService;
    }
    
    [HttpPost("request")]
    public async Task<IActionResult> SendFriendRequest([FromBody] FriendRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
            return UnauthorizedToken();
        
        var (isSuccess, errorMessage) = await _socialService.SendFriendRequestAsync(userId, request);

        if (!isSuccess)
            return ErrorResult(errorMessage);

        return Ok(new { message = "Заявка в друзья отправлена" });
    }

    [HttpGet("requests/incoming")]
    public async Task<ActionResult<List<AcceptFriendRequest>>> GetIncomingRequests()
    {
        if (!TryGetCurrentUserId(out var userId))
            return UnauthorizedToken<List<AcceptFriendRequest>>();
        
        var friendRequests = await _socialService.GetIncomingFriendRequestsAsync(userId);
        return Ok(friendRequests);
    }
    
    [HttpPost("accept")]
    public async Task<IActionResult> AcceptRequest([FromBody] AcceptFriendRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
            return UnauthorizedToken();
        
        var (isSuccess, errorMessage) = await _socialService.AcceptRequestAsync(userId, request);

        if (!isSuccess)
            return ErrorResult(errorMessage);

        return Ok(new { message = "Теперь вы друзья!" });
    }

    [HttpPost("decline")]
    public async Task<IActionResult> DeclineRequest([FromBody] AcceptFriendRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
            return UnauthorizedToken();
        
        var (isSuccess, errorMessage) = await _socialService.DeclineRequestAsync(userId, request);
        
        if (!isSuccess)
            return ErrorResult(errorMessage);
        
        return Ok(new { message = "Заявка отклонена" });
    }
    
    [HttpGet("me/friends")]
    public async Task<ActionResult<List<FriendDto>>> GetFriends()
    {
        if (!TryGetCurrentUserId(out var userId))
            return UnauthorizedToken<List<FriendDto>>();
        
        var friends = await _socialService.GetFriendsAsync(userId);
        return Ok(friends);
    }

    [HttpDelete("me/friends/{userId}")]
    public async Task<IActionResult> DeleteFriend(Guid userId)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
            return UnauthorizedToken();
        
        var (isSuccess, errorMessage) = await _socialService.DeleteFriendAsync(currentUserId, userId);

        if (!isSuccess)
        {
            return ErrorResult(errorMessage);
        }
        
        return Ok(new { isSuccess = true });
    }
}
