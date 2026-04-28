using System.ComponentModel.DataAnnotations;

namespace FlowMeet.Server.Models.Entities;

public class PasswordResetToken
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid UserId { get; set; }
    public User? User { get; set; }

    [Required]
    public string TokenHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
}
