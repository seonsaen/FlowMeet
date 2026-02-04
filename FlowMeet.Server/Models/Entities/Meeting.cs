using System.ComponentModel.DataAnnotations;

namespace FlowMeet.Server.Models.Entities;

public enum MeetingStatus
{
    Proposed = 0,
    Confirmed = 1,
    Cancelled = 2
}

public enum ParticipantStatus
{
    Pending = 0,
    Accepted = 1,
    Declined = 2
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
    
    public MeetingStatus Status { get; set; }

    public List<MeetingParticipant> Participants { get; set; } = new();
}

public class MeetingParticipant
{
    public Guid MeetingId { get; set; }
    public Meeting? Meeting { get; set; }

    public Guid UserId { get; set; }
    public User? User { get; set; }

    public ParticipantStatus Status { get; set; }
}