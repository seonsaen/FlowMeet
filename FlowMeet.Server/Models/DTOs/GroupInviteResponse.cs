using FlowMeet.Server.Models.Entities;

namespace FlowMeet.Server.Models.DTOs;

public class GroupInviteResponse
{
    public Guid GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public Guid InviterId { get; set; }
    public string InviterName { get; set; } = string.Empty;
    public GroupInviteStatus Status { get; set; }
    public DateTime CreatedDate { get; set; }
}
