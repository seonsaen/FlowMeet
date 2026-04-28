using System.ComponentModel.DataAnnotations;

namespace FlowMeet.Server.Models.Entities;

public enum NotificationType
{
    Info = 0,
    FriendRequest = 1,
    GroupInvite = 2,
    MeetingInvite = 3,
    Reminder = 4
}

public class Notification
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid UserId { get; set; }
    public User? User { get; set; }

    public NotificationType Type { get; set; } = NotificationType.Info;

    [Required]
    [MaxLength(150)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string Message { get; set; } = string.Empty;

    public Guid? RelatedEntityId { get; set; }
    public DateTime? ScheduledFor { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
}
