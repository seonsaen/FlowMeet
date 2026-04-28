namespace FlowMeet.Server.Models.Entities;

public enum GroupRole
{
    Owner = 0,
    Admin = 1,
    Member = 2
}

public class GroupMember
{
    public Guid GroupId { get; set; }
    public Group? Group { get; set; }

    public Guid UserId { get; set; }
    public User? User { get; set; }

    public GroupRole Role { get; set; }
    public DateTime JoinDate { get; set; } = DateTime.UtcNow;
}
