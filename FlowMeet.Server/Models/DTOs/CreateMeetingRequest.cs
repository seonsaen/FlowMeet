using System.ComponentModel.DataAnnotations;

namespace FlowMeet.Server.Models.DTOs;

public class CreateMeetingRequest
{
    [Required(ErrorMessage = "Укажите название встречи")]
    [MaxLength(150)]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required]
    public DateTime StartTime { get; set; }

    [Required]
    public DateTime EndTime { get; set; }

    [Required]
    [MinLength(1, ErrorMessage = "Встреча должна включать хотя бы одного приглашенного")]
    public List<Guid> ParticipantIds { get; set; } = new();

    public Guid? GroupId { get; set; }
}
