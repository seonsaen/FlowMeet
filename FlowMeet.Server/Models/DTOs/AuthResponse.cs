namespace FlowMeet.Server.Models.DTOs;

public class AuthResponse
{
    public string Token { get; set; }
    public string Email { get; set; }
    public string FirstName { get; set; }
}