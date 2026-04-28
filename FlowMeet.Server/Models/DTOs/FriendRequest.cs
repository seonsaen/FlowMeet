using System.ComponentModel.DataAnnotations;

namespace FlowMeet.Server.Models.DTOs;

public class FriendRequest
{
    [Required(ErrorMessage = "Email целевого пользователя обязателен")]
    [EmailAddress(ErrorMessage = "Неверный формат Email")]
    public string TargetEmail { get; set; } = string.Empty;
}

public class AcceptFriendRequest
{
    [Required]
    public Guid RequesterId { get; set; }
    
    public string RequesterName { get; set; } = string.Empty;
    
    public string RequesterEmail { get; set; } = string.Empty;
}