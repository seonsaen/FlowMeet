using System.ComponentModel.DataAnnotations;

namespace FlowMeet.Server.Models.DTOs;

public class CreateGroupRequest
{
    [Required(ErrorMessage = "Название группы обязательно")]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }
}