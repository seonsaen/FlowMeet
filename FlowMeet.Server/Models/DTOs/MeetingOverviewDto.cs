using FlowMeet.Server.Models.Entities;

namespace FlowMeet.Server.Models.DTOs;

public class MeetingOverviewDto
{
    public Guid MeetingId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public MeetingStatus Status { get; set; }
    public Guid OrganizerId { get; set; }
    public string OrganizerName { get; set; } = string.Empty;
    public Guid? RelatedGroupId { get; set; }
    public string? RelatedGroupName { get; set; }
    public bool CanEdit { get; set; }
    public List<Guid> ParticipantIds { get; set; } = new();
    public List<string> ParticipantNames { get; set; } = new();
}

public class ScheduledMeetingCardDto
{
    public Guid MeetingId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public Guid? RelatedGroupId { get; set; }
    public string? RelatedGroupName { get; set; }
}
