namespace FlowMeet.Server.Models.DTOs;

public class TimeSlotDto
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    
    public string Suitability { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}