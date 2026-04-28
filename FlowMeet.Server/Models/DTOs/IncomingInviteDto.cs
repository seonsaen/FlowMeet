namespace FlowMeet.Server.Models.DTOs;

public class IncomingInviteDto
{
    public Guid MeetingId { get; set; }
    public string OrganizerName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartTime { get; set; }
}