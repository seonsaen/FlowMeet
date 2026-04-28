using FlowMeet.Server.Data;
using FlowMeet.Server.Models.Entities;
using FlowMeet.Server.Models.DTOs;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace FlowMeet.Server.Services;

public class AuthService : IAuthService
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;

    public AuthService(AppDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public async Task<(bool IsSuccess, string ErrorMessage)> RegisterAsync(RegisterRequest request)
    {
        // Проверка есть ли такой email
        if (await _context.Users.AnyAsync(u => u.Email == request.Email))
        {
            return (false, "Пользователь с таким email уже существует");
        }

        // Создание пользователя
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password), // Хэширование
            FirstName = request.FirstName,
            LastName = request.LastName,
            SettingsJson = "{}"
        };

        // Сохранение в БД
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return (true, string.Empty);
    }

    public async Task<AuthResponse?> LoginAsync(LoginRequest request)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return null; // Пароль не совпал или пользователя нет
        }

        var token = GenerateJwtToken(user);

        return new AuthResponse 
        { 
            Id = user.Id,
            Token = token, 
            Email = user.Email, 
            FirstName = user.FirstName 
        };
    }

    public async Task<PasswordResetRequestResponse> RequestPasswordResetAsync(PasswordResetRequest request)
    {
        var response = new PasswordResetRequestResponse
        {
            Message = "Если пользователь с таким email существует, инструкция по восстановлению будет отправлена"
        };

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
        if (user == null)
            return response;

        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');

        var resetToken = new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = HashToken(token),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };

        _context.PasswordResetTokens.Add(resetToken);
        await _context.SaveChangesAsync();

        // В проекте пока нет email-провайдера, поэтому возвращаем токен для клиента/dev-сценария.
        response.ResetToken = token;
        return response;
    }

    public async Task<(bool IsSuccess, string ErrorMessage)> ConfirmPasswordResetAsync(PasswordResetConfirmRequest request)
    {
        var tokenHash = HashToken(request.Token);
        var resetToken = await _context.PasswordResetTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

        if (resetToken == null || resetToken.User == null)
            return (false, "Токен восстановления недействителен");

        if (resetToken.UsedAt.HasValue)
            return (false, "Токен восстановления уже использован");

        if (resetToken.ExpiresAt < DateTime.UtcNow)
            return (false, "Срок действия токена восстановления истек");

        resetToken.User.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        resetToken.UsedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return (true, string.Empty);
    }

    private string GenerateJwtToken(User user)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.FirstName)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }
}
