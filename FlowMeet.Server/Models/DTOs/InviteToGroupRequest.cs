using System.ComponentModel.DataAnnotations;

namespace FlowMeet.Server.Models.DTOs;

public class InviteToGroupRequest
{
    [Required]
    public Guid GroupId { get; set; }

    [Required]
    public Guid InviteeId { get; set; }
}
