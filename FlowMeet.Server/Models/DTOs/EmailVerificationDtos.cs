using System.ComponentModel.DataAnnotations;

namespace FlowMeet.Server.Models.DTOs;

public class ConfirmRegistrationRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(6, MinimumLength = 6)]
    public string Code { get; set; } = string.Empty;
}

public class EmailChangeRequest
{
    [Required]
    [EmailAddress]
    public string NewEmail { get; set; } = string.Empty;
}

public class ConfirmEmailChangeRequest
{
    [Required]
    [EmailAddress]
    public string NewEmail { get; set; } = string.Empty;

    [Required]
    [StringLength(6, MinimumLength = 6)]
    public string Code { get; set; } = string.Empty;
}

public class ChangePasswordRequest
{
    [Required]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required]
    [MinLength(6)]
    public string NewPassword { get; set; } = string.Empty;
}
