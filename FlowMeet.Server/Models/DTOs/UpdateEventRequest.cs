using System.ComponentModel.DataAnnotations;
using FlowMeet.Server.Models.Entities;

namespace FlowMeet.Server.Models.DTOs;

public class UpdateEventRequest
{
    [Required(ErrorMessage = "Название события обязательно")]
    [MaxLength(100, ErrorMessage = "Название не может быть длиннее 100 символов")]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required(ErrorMessage = "Время начала обязательно")]
    public DateTime StartTime { get; set; }

    [Required(ErrorMessage = "Время окончания обязательно")]
    public DateTime EndTime { get; set; }

    [Required(ErrorMessage = "Тип занятости обязателен")]
    public EventType Type { get; set; }
}