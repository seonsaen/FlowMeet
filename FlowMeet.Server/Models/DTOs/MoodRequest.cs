using System.ComponentModel.DataAnnotations;
using FlowMeet.Server.Models.Entities;

namespace FlowMeet.Server.Models.DTOs;

public class MoodRequest
{
    [Required]
    [Range(1, 5, ErrorMessage = "Настроение должно быть от 1 до 5")]
    public int MoodLevel { get; set; }

    [Range(0, 2, ErrorMessage = "Качество сна должно быть от 0 до 2")]
    public SleepQuality SleepQuality { get; set; } = SleepQuality.Normal;

    [Range(0, 2, ErrorMessage = "Фоновая нагрузка должна быть от 0 до 2")]
    public BackgroundLoadLevel BackgroundLoadLevel { get; set; } = BackgroundLoadLevel.Calm;
}
