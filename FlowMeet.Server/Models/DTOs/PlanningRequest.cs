namespace FlowMeet.Server.Models.DTOs;

public class PlanningRequest
{
    // Список всех, кто идет на встречу
    public List<Guid> ParticipantIds { get; set; } = new();
    public DateOnly StartDate { get; set; } 
    public int DurationMinutes { get; set; } = 60;
}