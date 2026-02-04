using System.ComponentModel.DataAnnotations;

namespace FlowMeet.Server.Models.Entities;

public class UserState
{
    [Key]
    public int Id { get; set; }

    [Required]
    public Guid UserId { get; set; }
    public User? User { get; set; }

    [Required]
    public DateOnly Date { get; set; }

    [Range(1, 5)]
    public int MoodLevel { get; set; } // Оценка настроения 1-5

    [Range(0, 100)]
    public int ResourceLevel { get; set; } // Вычисляемый ресурс

    public string? Note { get; set; }
}