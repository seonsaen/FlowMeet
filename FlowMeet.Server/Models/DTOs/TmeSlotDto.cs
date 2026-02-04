namespace FlowMeet.Server.Models.DTOs;

public class TimeSlotDto
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    
    // Качество слота: "Optimal" (Идеально), "Hard" (У кого-то мало сил), "RequiresMoving" (Нужно двигать гибкие дела)
    public string Suitability { get; set; } 
    public string Description { get; set; }
}