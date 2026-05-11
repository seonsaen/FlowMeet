namespace FlowMeet.Server.Models.DTOs;

public class IncomingInviteDto
{
    public Guid MeetingId { get; set; }
    public string OrganizerName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
}

public class OutgoingInviteDto
{
    public Guid MeetingId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int PendingParticipantsCount { get; set; }
    public int AcceptedParticipantsCount { get; set; }
    public int TotalParticipantsCount { get; set; }
    public List<string> PendingParticipantNames { get; set; } = new();
}
