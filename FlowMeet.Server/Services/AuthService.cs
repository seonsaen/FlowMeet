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
using Microsoft.Extensions.Options;

namespace FlowMeet.Server.Services;

public class AuthService : IAuthService
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly IEmailService _emailService;
    private readonly SmtpEmailOptions _smtpOptions;

    public AuthService(
        AppDbContext context,
        IConfiguration configuration,
        IEmailService emailService,
        IOptions<SmtpEmailOptions> smtpOptions)
    {
        _context = context;
        _configuration = configuration;
        _emailService = emailService;
        _smtpOptions = smtpOptions.Value;
    }

    public async Task<(bool IsSuccess, string ErrorMessage)> RegisterAsync(RegisterRequest request)
    {
        if (await _context.Users.AnyAsync(u => u.Email == request.Email))
            return (false, "Пользователь с таким email уже существует");

        var code = GenerateVerificationCode();
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        await UpsertEmailVerificationCodeAsync(
            request.Email,
            EmailVerificationPurpose.Registration,
            code,
            userId: null,
            request.FirstName,
            request.LastName,
            passwordHash);

        await SendVerificationCodeAsync(
            request.Email,
            "Подтверждение регистрации FlowMeet",
            $"""
            Подтверждение регистрации FlowMeet

            Код подтверждения: {code}

            Код действителен 15 минут.
            """);

        return (true, string.Empty);
    }

    public async Task<(bool IsSuccess, string ErrorMessage)> ConfirmRegistrationAsync(ConfirmRegistrationRequest request)
    {
        if (await _context.Users.AnyAsync(user => user.Email == request.Email))
            return (false, "Пользователь с таким email уже существует");

        var verificationCode = await _context.EmailVerificationCodes
            .Where(code => code.Email == request.Email
                           && code.Purpose == EmailVerificationPurpose.Registration
                           && code.UsedAt == null
                           && code.ExpiresAt >= DateTime.UtcNow)
            .OrderByDescending(code => code.CreatedAt)
            .FirstOrDefaultAsync(code => code.CodeHash == HashCode(request.Code));

        if (verificationCode == null || string.IsNullOrWhiteSpace(verificationCode.PendingPasswordHash))
            return (false, "Код подтверждения недействителен или истёк");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = verificationCode.Email,
            PasswordHash = verificationCode.PendingPasswordHash,
            FirstName = verificationCode.FirstName,
            LastName = verificationCode.LastName,
            SettingsJson = "{}"
        };

        verificationCode.UsedAt = DateTime.UtcNow;
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return (true, string.Empty);
    }

    public async Task<AuthResponse?> LoginAsync(LoginRequest request)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return null;
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

    public async Task<PasswordResetRequestResponse> RequestPasswordResetAsync(PasswordResetRequest request, CancellationToken cancellationToken = default)
    {
        var response = new PasswordResetRequestResponse
        {
            Message = "Если пользователь с таким email существует, код восстановления будет отправлен"
        };

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email, cancellationToken);
        if (user == null)
            return response;

        var code = GenerateVerificationCode();
        await UpsertEmailVerificationCodeAsync(
            user.Email,
            EmailVerificationPurpose.PasswordReset,
            code,
            user.Id,
            firstName: null,
            lastName: null,
            pendingPasswordHash: null,
            cancellationToken);

        var subject = "Восстановление пароля FlowMeet";
        var textBody = $"""
            Вы запросили восстановление пароля для FlowMeet.

            Код подтверждения: {code}

            Код действителен 15 минут.
            """;
        await _emailService.SendAsync(user.Email, subject, textBody, cancellationToken);

        return response;
    }

    public async Task<(bool IsSuccess, string ErrorMessage)> ConfirmPasswordResetAsync(PasswordResetConfirmRequest request)
    {
        var codeHash = HashCode(request.Code);
        var resetCode = await _context.EmailVerificationCodes
            .Include(code => code.User)
            .Where(code => code.Email == request.Email
                           && code.Purpose == EmailVerificationPurpose.PasswordReset
                           && code.UsedAt == null
                           && code.ExpiresAt >= DateTime.UtcNow)
            .OrderByDescending(code => code.CreatedAt)
            .FirstOrDefaultAsync(code => code.CodeHash == codeHash);

        if (resetCode == null || resetCode.User == null)
            return (false, "Код восстановления недействителен или истек");

        resetCode.User.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        resetCode.UsedAt = DateTime.UtcNow;
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

    private async Task UpsertEmailVerificationCodeAsync(
        string email,
        EmailVerificationPurpose purpose,
        string rawCode,
        Guid? userId,
        string? firstName,
        string? lastName,
        string? pendingPasswordHash,
        CancellationToken cancellationToken = default)
    {
        var existingCodes = await _context.EmailVerificationCodes
            .Where(code => code.Purpose == purpose
                           && code.UsedAt == null
                           && ((userId != null && code.UserId == userId) || code.Email == email))
            .ToListAsync(cancellationToken);

        if (existingCodes.Count > 0)
            _context.EmailVerificationCodes.RemoveRange(existingCodes);

        _context.EmailVerificationCodes.Add(new EmailVerificationCode
        {
            Id = Guid.NewGuid(),
            Email = email,
            UserId = userId,
            Purpose = purpose,
            CodeHash = HashCode(rawCode),
            PendingPasswordHash = pendingPasswordHash,
            FirstName = firstName ?? string.Empty,
            LastName = lastName ?? string.Empty,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15)
        });

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task SendVerificationCodeAsync(string email, string subject, string textBody, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_smtpOptions.Host))
        {
            return;
        }

        await _emailService.SendAsync(email, subject, textBody, cancellationToken);
    }

    private static string GenerateVerificationCode() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    private static string HashCode(string code)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(code));
        return Convert.ToHexString(bytes);
    }
}
