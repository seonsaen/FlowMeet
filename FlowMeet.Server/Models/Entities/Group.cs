using System.ComponentModel.DataAnnotations;

namespace FlowMeet.Server.Models.Entities;

public class Group
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public List<GroupMember> Members { get; set; } = new();
}

public class GroupMember
{
    public Guid GroupId { get; set; }
    public Group? Group { get; set; }

    public Guid UserId { get; set; }
    public User? User { get; set; }
    
    public bool IsAdmin { get; set; }
}