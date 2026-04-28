using System.ComponentModel.DataAnnotations;

namespace FlowMeet.Server.Models.DTOs;

public class PasswordResetRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}

public class PasswordResetRequestResponse
{
    public string Message { get; set; } = string.Empty;
    public string? ResetToken { get; set; }
}

public class PasswordResetConfirmRequest
{
    [Required]
    public string Token { get; set; } = string.Empty;

    [Required]
    [MinLength(6)]
    public string NewPassword { get; set; } = string.Empty;
}
