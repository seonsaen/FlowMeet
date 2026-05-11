using System.ComponentModel.DataAnnotations;

namespace FlowMeet.Server.Models.Entities;

public enum EmailVerificationPurpose
{
    Registration = 0,
    EmailChange = 1,
    PasswordReset = 2
}

public class EmailVerificationCode
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    public Guid? UserId { get; set; }
    public User? User { get; set; }

    public EmailVerificationPurpose Purpose { get; set; }

    [Required]
    public string CodeHash { get; set; } = string.Empty;

    public string? PendingPasswordHash { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
}