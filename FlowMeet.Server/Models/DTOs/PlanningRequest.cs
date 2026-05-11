using System.ComponentModel.DataAnnotations;

namespace FlowMeet.Server.Models.DTOs;

public class PlanningRequest
{
    public List<Guid> ParticipantIds { get; set; } = new();
    public DateOnly StartDate { get; set; } 
    [Range(15, 720, ErrorMessage = "Длительность встречи должна быть от 15 до 720 минут")]
    public int DurationMinutes { get; set; } = 60;
}