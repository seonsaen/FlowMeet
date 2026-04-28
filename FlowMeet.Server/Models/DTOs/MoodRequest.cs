using System.ComponentModel.DataAnnotations;

namespace FlowMeet.Server.Models.DTOs;

public class MoodRequest
{
    [Required]
    [Range(1, 5, ErrorMessage = "Настроение должно быть от 1 до 5")]
    public int MoodLevel { get; set; }
}