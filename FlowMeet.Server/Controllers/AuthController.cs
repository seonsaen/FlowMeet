using FlowMeet.Server.Models.DTOs;
using Microsoft.AspNetCore.Mvc;
using FlowMeet.Server.Services;

namespace FlowMeet.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    
    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var (isSuccess, errorMessage) = await _authService.RegisterAsync(request);

        if (!isSuccess)
        {
            return BadRequest(new { error = errorMessage });
        }

        return Ok(new { message = "Регистрация успешна" });
    }
    
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var authResponse = await _authService.LoginAsync(request);

        if (authResponse == null)
        {
            return Unauthorized(new { error = "Неверный логин или пароль" });
        }
        
        return Ok(authResponse);
    }

    [HttpPost("password-reset/request")]
    public async Task<ActionResult<PasswordResetRequestResponse>> RequestPasswordReset([FromBody] PasswordResetRequest request)
    {
        var response = await _authService.RequestPasswordResetAsync(request);
        return Ok(response);
    }

    [HttpPost("password-reset/confirm")]
    public async Task<IActionResult> ConfirmPasswordReset([FromBody] PasswordResetConfirmRequest request)
    {
        var (isSuccess, errorMessage) = await _authService.ConfirmPasswordResetAsync(request);

        if (!isSuccess)
            return BadRequest(new { error = errorMessage });

        return Ok(new { message = "Пароль успешно обновлен" });
    }
}
