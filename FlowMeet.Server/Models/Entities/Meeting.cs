using System.ComponentModel.DataAnnotations;

namespace FlowMeet.Server.Models.Entities;

public enum MeetingStatus
{
    Proposed = 0,
    Confirmed = 1,
    Cancelled = 2
}

public class Meeting
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    public DateTime StartTime { get; set; }
    public TimeSpan Duration { get; set; }

    public Guid InitiatorId { get; set; }
    public User? Initiator { get; set; }

    public Guid? RelatedGroupId { get; set; }
    public Group? RelatedGroup { get; set; }
    
    public MeetingStatus Status { get; set; }

    public List<MeetingInvite> Participants { get; set; } = new();
}