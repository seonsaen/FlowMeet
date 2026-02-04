using FlowMeet.Server.Models.Entities;

namespace FlowMeet.Server.Models.DTOs;

public class EventResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string Type { get; set; }
}