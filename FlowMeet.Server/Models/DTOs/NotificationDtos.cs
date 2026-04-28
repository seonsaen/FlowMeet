using System.ComponentModel.DataAnnotations;
using FlowMeet.Server.Models.Entities;

namespace FlowMeet.Server.Models.DTOs;

public class NotificationDto
{
    public Guid Id { get; set; }
    public NotificationType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Guid? RelatedEntityId { get; set; }
    public DateTime? ScheduledFor { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
    public bool IsRead => ReadAt.HasValue;
}

public class CreateReminderRequest
{
    [Required]
    [MaxLength(150)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string Message { get; set; } = string.Empty;

    [Required]
    public DateTime ScheduledFor { get; set; }

    public Guid? RelatedEntityId { get; set; }
}
