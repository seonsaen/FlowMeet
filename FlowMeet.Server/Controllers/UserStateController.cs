using System.Security.Claims;
using FlowMeet.Server.Models.DTOs;
using FlowMeet.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowMeet.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UserStateController : ControllerBase
{
    private readonly IUserStateService _userStateService;
    
    public UserStateController(IUserStateService userStateService)
    {
        _userStateService = userStateService;
    }
    
    private bool TryGetCurrentUserId(out Guid userId)
    {
        userId = Guid.Empty;

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return !string.IsNullOrWhiteSpace(userIdClaim) && Guid.TryParse(userIdClaim, out userId);
    }

    [HttpPost("mood")]
    public async Task<IActionResult> SetMood([FromBody] MoodRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized(new { error = "Некорректный токен" });
        
        var (resourceLevel, message) = await _userStateService.SetMoodAsync(userId, request);
        
        return Ok(new { Message = message, CurrentResource = resourceLevel });
    }

    [HttpGet("me/resource")]
    public async Task<ActionResult<ResourceResponse>> GetResource()
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized(new { error = "Некорректный токен" });
        
        var response = await _userStateService.GetResourceStatusAsync(userId);
        return Ok(response);
    }
}