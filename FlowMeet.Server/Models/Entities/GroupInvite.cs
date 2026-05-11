namespace FlowMeet.Server.Models.Entities;

public enum GroupInviteStatus
{
    Pending = 0,
    Accepted = 1,
    Declined = 2
}

public class GroupInvite
{
    public Guid GroupId { get; set; }
    public Group? Group { get; set; }

    public Guid InviteeId { get; set; }
    public User? Invitee { get; set; }

    public Guid InviterId { get; set; }
    public User? Inviter { get; set; }

    public GroupInviteStatus Status { get; set; } = GroupInviteStatus.Pending;
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}