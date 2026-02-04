using System.ComponentModel.DataAnnotations;

namespace FlowMeet.Server.Models.Entities;

public enum EventType
{
    Mandatory = 0, // Обязательное
    Flexible = 1,  // Опциональное/Переносимое
    Desirable = 2  // Желательное
}

public class Event
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid UserId { get; set; }
    public User? User { get; set; }

    [Required]
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }

    public EventType Type { get; set; }

    // Если true, это событие-шаблон, которое копируется на следующую неделю
    public bool IsTemplate { get; set; } 
    
    // День недели для шаблонных событий
    public DayOfWeek? TemplateDayOfWeek { get; set; }
}