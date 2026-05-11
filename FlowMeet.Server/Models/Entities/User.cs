using System.ComponentModel.DataAnnotations;

namespace FlowMeet.Server.Models.Entities;

public class User
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    
    public string SettingsJson { get; set; } = "{}";
    
    public List<Event> Events { get; set; } = new();
    public List<UserState> States { get; set; } = new();
    public List<Friendship> SentFriendRequests { get; set; } = new();
    public List<Friendship> ReceivedFriendRequests { get; set; } = new();
    public List<EmailVerificationCode> EmailVerificationCodes { get; set; } = new();
}