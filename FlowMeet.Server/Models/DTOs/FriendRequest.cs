namespace FlowMeet.Server.Models.DTOs;

public class FriendRequest
{
    public Guid RequesterId { get; set; }
    public string TargetEmail { get; set; }
}