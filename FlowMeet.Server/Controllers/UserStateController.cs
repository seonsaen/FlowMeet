using FlowMeet.Server.Models.DTOs;
using FlowMeet.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowMeet.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UserStateController : AuthorizedApiController
{
    private readonly IUserStateService _userStateService;
    
    public UserStateController(IUserStateService userStateService)
    {
        _userStateService = userStateService;
    }
    
    [HttpPost("mood")]
    public async Task<IActionResult> SetMood([FromBody] MoodRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
            return UnauthorizedToken();
        
        var (resourceLevel, message) = await _userStateService.SetMoodAsync(userId, request);
        
        return Ok(new { Message = message, CurrentResource = resourceLevel });
    }

    [HttpGet("me/resource")]
    public async Task<ActionResult<ResourceResponse>> GetResource()
    {
        if (!TryGetCurrentUserId(out var userId))
            return UnauthorizedToken<ResourceResponse>();
        
        var response = await _userStateService.GetResourceStatusAsync(userId);
        return Ok(response);
    }
}
