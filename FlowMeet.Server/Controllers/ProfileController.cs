using System.Security.Claims;
using FlowMeet.Server.Models.DTOs;
using FlowMeet.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowMeet.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly IProfileService _profileService;

    public ProfileController(IProfileService profileService)
    {
        _profileService = profileService;
    }
    
    private bool TryGetCurrentUserId(out Guid userId)
    {
        userId = Guid.Empty;

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return !string.IsNullOrWhiteSpace(userIdClaim) && Guid.TryParse(userIdClaim, out userId);
    }

    [HttpGet("me")]
    public async Task<ActionResult<ProfileResponse>> GetProfile()
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized(new { error = "Некорректный токен" });
        
        var (isSuccess, errorMessage, profile) = await _profileService.GetProfileAsync(userId);
        if (!isSuccess) return NotFound(new { error = errorMessage });

        return Ok(profile);
    }

    [HttpPut("me")]
    public async Task<ActionResult<ProfileResponse>> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized(new { error = "Некорректный токен" });
        
        var (isSuccess, errorMessage, profile) = await _profileService.UpdateProfileAsync(userId, request);
        if (!isSuccess) return NotFound(new { error = errorMessage });

        return Ok(profile);
    }

    [HttpGet("me/base-schedule")]
    public async Task<ActionResult<List<BaseScheduleEntryDto>>> GetBaseSchedule()
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized(new { error = "Некорректный токен" });
        
        var schedule = await _profileService.GetBaseScheduleAsync(userId);
        return Ok(schedule);
    }

    [HttpPut("me/base-schedule")]
    public async Task<ActionResult<List<BaseScheduleEntryDto>>> UpdateBaseSchedule([FromBody] List<BaseScheduleEntryDto> entries)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized(new { error = "Некорректный токен" });
        
        var (isSuccess, errorMessage, schedule) = await _profileService.UpdateBaseScheduleAsync(userId, entries);
        
        if (!isSuccess) return BadRequest(new { error = errorMessage });

        return Ok(schedule);
    }
}