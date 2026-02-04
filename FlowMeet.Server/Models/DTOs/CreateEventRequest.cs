using FlowMeet.Server.Models.Entities;

namespace FlowMeet.Server.Models.DTOs;

public class CreateEventRequest
{
    public Guid UserId { get; set; } 
    public string Title { get; set; }
    public string Description { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public EventType Type { get; set; }
}