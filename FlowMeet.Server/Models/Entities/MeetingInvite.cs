namespace FlowMeet.Server.Models.Entities;

public enum ParticipantStatus
{
    Pending = 0,
    Accepted = 1,
    Declined = 2
}

public class MeetingInvite
{
    public Guid MeetingId { get; set; }
    public Meeting? Meeting { get; set; }

    public Guid UserId { get; set; }
    public User? User { get; set; }

    public ParticipantStatus Status { get; set; }
}