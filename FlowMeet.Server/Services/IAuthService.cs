using FlowMeet.Server.Models.DTOs;

namespace FlowMeet.Server.Services;

public interface IAuthService
{
    Task<(bool IsSuccess, string ErrorMessage)> RegisterAsync(RegisterRequest request);
    
    Task<AuthResponse?> LoginAsync(LoginRequest request);
    Task<PasswordResetRequestResponse> RequestPasswordResetAsync(PasswordResetRequest request);
    Task<(bool IsSuccess, string ErrorMessage)> ConfirmPasswordResetAsync(PasswordResetConfirmRequest request);
}
