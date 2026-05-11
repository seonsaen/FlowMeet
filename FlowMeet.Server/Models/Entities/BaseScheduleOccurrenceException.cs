using System.ComponentModel.DataAnnotations;

namespace FlowMeet.Server.Models.Entities;

public class BaseScheduleOccurrenceException
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid UserId { get; set; }
    public User? User { get; set; }

    [Required]
    public Guid BaseScheduleEntryId { get; set; }
    public BaseScheduleEntry? BaseScheduleEntry { get; set; }

    [Required]
    public DateOnly Date { get; set; }

    public Guid? OverrideEventId { get; set; }
    public Event? OverrideEvent { get; set; }
}