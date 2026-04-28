namespace FlowMeet.Server.Models.DTOs;

public class GroupIncomingInviteDto
{
    public Guid GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public Guid InviterId { get; set; }
    public string InviterName { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
}