using System.ComponentModel.DataAnnotations;

namespace FlowMeet.Server.Models.DTOs;

public class RespondToGroupInviteRequest
{
    [Required]
    public Guid GroupId { get; set; }

    [Required]
    public bool IsAccepted { get; set; }
}