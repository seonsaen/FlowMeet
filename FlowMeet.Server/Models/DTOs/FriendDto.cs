namespace FlowMeet.Server.Models.DTOs;

public class FriendDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsBusy { get; set; }
    public TimeSlotDto? NearestAvailableSlot { get; set; }
    public ScheduledMeetingCardDto? UpcomingMeeting { get; set; }
    public TimeSlotDto? EarlierAvailableSlot { get; set; }
}
