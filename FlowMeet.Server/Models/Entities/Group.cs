using System.ComponentModel.DataAnnotations;

namespace FlowMeet.Server.Models.Entities;

public class Group
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid OwnerId { get; set; }
    public User? Owner { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public DateTime CreatedDate { get; set; }

    public List<GroupMember> Members { get; set; } = new();
}