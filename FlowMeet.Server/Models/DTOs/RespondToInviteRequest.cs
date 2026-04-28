using System.ComponentModel.DataAnnotations;

namespace FlowMeet.Server.Models.DTOs;

public class RespondToInviteRequest
{
    [Required]
    public Guid MeetingId { get; set; }
    
    [Required]
    public bool IsAccepted { get; set; } // true - Принять, false - Отклонить
}