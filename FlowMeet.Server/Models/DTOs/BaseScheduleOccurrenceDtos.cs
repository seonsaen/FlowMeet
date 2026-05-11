using System.ComponentModel.DataAnnotations;
using FlowMeet.Server.Models.Entities;

namespace FlowMeet.Server.Models.DTOs;

public class BaseScheduleOccurrenceExceptionDto
{
    public Guid Id { get; set; }
    public Guid BaseScheduleEntryId { get; set; }
    public DateOnly Date { get; set; }
    public Guid? OverrideEventId { get; set; }
}

public class OverrideBaseScheduleOccurrenceRequest
{
    [Required]
    public Guid BaseScheduleEntryId { get; set; }

    [Required]
    public DateOnly OccurrenceDate { get; set; }

    [Required]
    [MaxLength(100, ErrorMessage = "Название не может быть длиннее 100 символов")]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required]
    public DateTime StartTime { get; set; }

    [Required]
    public DateTime EndTime { get; set; }

    [Required]
    public EventType Type { get; set; }
}

public class CancelBaseScheduleOccurrenceRequest
{
    [Required]
    public Guid BaseScheduleEntryId { get; set; }

    [Required]
    public DateOnly OccurrenceDate { get; set; }
}
