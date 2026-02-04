namespace FlowMeet.Server.Models.DTOs;

public class MoodRequest
{
    public Guid UserId { get; set; }
    public int MoodLevel { get; set; } 
}