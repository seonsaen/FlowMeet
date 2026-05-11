using FlowMeet.Server.Models.DTOs;
using FlowMeet.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowMeet.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProfileController : AuthorizedApiController
{
    private readonly IProfileService _profileService;

    public ProfileController(IProfileService profileService)
    {
        _profileService = profileService;
    }
    
    [HttpGet("me")]
    public async Task<ActionResult<ProfileResponse>> GetProfile()
    {
        if (!TryGetCurrentUserId(out var userId))
            return UnauthorizedToken<ProfileResponse>();
        
        var (isSuccess, errorMessage, profile) = await _profileService.GetProfileAsync(userId);
        if (!isSuccess) return ErrorResult<ProfileResponse>(errorMessage);

        return Ok(profile);
    }

    [HttpPut("me")]
    public async Task<ActionResult<ProfileResponse>> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
            return UnauthorizedToken<ProfileResponse>();
        
        var (isSuccess, errorMessage, profile) = await _profileService.UpdateProfileAsync(userId, request);
        if (!isSuccess) return ErrorResult<ProfileResponse>(errorMessage);

        return Ok(profile);
    }

    [HttpPost("me/email-change/request")]
    public async Task<IActionResult> RequestEmailChange([FromBody] EmailChangeRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return UnauthorizedToken();

        var (isSuccess, errorMessage) = await _profileService.RequestEmailChangeAsync(userId, request.NewEmail, cancellationToken);
        if (!isSuccess)
            return ErrorResult(errorMessage);

        return Ok(new { message = "Код подтверждения отправлен на новую почту" });
    }

    [HttpPost("me/email-change/confirm")]
    public async Task<ActionResult<ProfileResponse>> ConfirmEmailChange([FromBody] ConfirmEmailChangeRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
            return UnauthorizedToken<ProfileResponse>();

        var (isSuccess, errorMessage, profile) = await _profileService.ConfirmEmailChangeAsync(userId, request);
        if (!isSuccess)
            return ErrorResult<ProfileResponse>(errorMessage);

        return Ok(profile);
    }

    [HttpPost("me/change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
            return UnauthorizedToken();

        var (isSuccess, errorMessage) = await _profileService.ChangePasswordAsync(userId, request);
        if (!isSuccess)
            return ErrorResult(errorMessage);

        return Ok(new { message = "Пароль обновлён" });
    }

    [HttpGet("me/base-schedule")]
    public async Task<ActionResult<List<BaseScheduleEntryDto>>> GetBaseSchedule()
    {
        if (!TryGetCurrentUserId(out var userId))
            return UnauthorizedToken<List<BaseScheduleEntryDto>>();
        
        var schedule = await _profileService.GetBaseScheduleAsync(userId);
        return Ok(schedule);
    }

    [HttpGet("me/base-schedule/history")]
    public async Task<ActionResult<List<BaseScheduleEntryDto>>> GetBaseScheduleHistory([FromQuery] DateOnly fromDate, [FromQuery] DateOnly toDate)
    {
        if (!TryGetCurrentUserId(out var userId))
            return UnauthorizedToken<List<BaseScheduleEntryDto>>();

        if (toDate < fromDate)
            return BadRequest(new { message = "Диапазон дат задан неверно" });

        var schedule = await _profileService.GetBaseScheduleHistoryAsync(userId, fromDate, toDate);
        return Ok(schedule);
    }

    [HttpGet("me/base-schedule/exceptions")]
    public async Task<ActionResult<List<BaseScheduleOccurrenceExceptionDto>>> GetBaseScheduleExceptions()
    {
        if (!TryGetCurrentUserId(out var userId))
            return UnauthorizedToken<List<BaseScheduleOccurrenceExceptionDto>>();

        var exceptions = await _profileService.GetBaseScheduleExceptionsAsync(userId);
        return Ok(exceptions);
    }

    [HttpPut("me/base-schedule")]
    public async Task<ActionResult<List<BaseScheduleEntryDto>>> UpdateBaseSchedule([FromBody] List<BaseScheduleEntryDto> entries)
    {
        if (!TryGetCurrentUserId(out var userId))
            return UnauthorizedToken<List<BaseScheduleEntryDto>>();
        
        var (isSuccess, errorMessage, schedule) = await _profileService.UpdateBaseScheduleAsync(userId, entries);
        
        if (!isSuccess) return ErrorResult<List<BaseScheduleEntryDto>>(errorMessage);

        return Ok(schedule);
    }
}
