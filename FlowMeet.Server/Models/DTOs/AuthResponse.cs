namespace FlowMeet.Server.Models.DTOs;

public class AuthResponse
{
    public Guid Id { get; set; }
    public string Token { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
}