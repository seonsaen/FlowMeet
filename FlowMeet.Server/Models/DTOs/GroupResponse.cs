using FlowMeet.Server.Models.Entities;

namespace FlowMeet.Server.Models.DTOs;

public class GroupResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<GroupMemberResponse> Members { get; set; } = new();
}

public class GroupMemberResponse
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public GroupRole Role { get; set; }
    public bool IsOwner { get; set; }
    public DateTime JoinDate { get; set; }
}