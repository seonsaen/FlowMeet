using System.Security.Cryptography;
using System.Text;
using FlowMeet.Server.Models.DTOs;
using FlowMeet.Server.Models.Entities;
using FlowMeet.Server.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace FlowMeet.Server.Tests;

public class AuthServiceTests
{
    [Fact]
    public async Task RequestPasswordResetAsync_DoesNotReturnCodeAndSendsEmail()
    {
        await using var context = TestDbFactory.CreateContext();
        var emailService = new TestEmailService();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "user@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("old-password"),
            FirstName = "Test",
            LastName = "User"
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        var service = CreateAuthService(context, emailService);

        var response = await service.RequestPasswordResetAsync(new PasswordResetRequest
        {
            Email = user.Email
        });

        var codeEntity = context.EmailVerificationCodes.Single();

        Assert.Equal("Если пользователь с таким email существует, код восстановления будет отправлен", response.Message);
        Assert.Equal(user.Id, codeEntity.UserId);
        Assert.Equal(user.Email, codeEntity.Email);
        Assert.Equal(EmailVerificationPurpose.PasswordReset, codeEntity.Purpose);
        Assert.False(string.IsNullOrWhiteSpace(codeEntity.CodeHash));
        Assert.Single(emailService.SentEmails);
        Assert.Matches(@"\b\d{6}\b", emailService.SentEmails[0].TextBody);
        Assert.DoesNotContain("token=", emailService.SentEmails[0].TextBody);
    }

    [Fact]
    public async Task ConfirmPasswordResetAsync_UpdatesPasswordAndMarksCodeUsed()
    {
        await using var context = TestDbFactory.CreateContext();
        var emailService = new TestEmailService();
        var plainCode = "123456";
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "user@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("old-password"),
            FirstName = "Test",
            LastName = "User"
        };

        context.Users.Add(user);
        context.EmailVerificationCodes.Add(new EmailVerificationCode
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Email = user.Email,
            Purpose = EmailVerificationPurpose.PasswordReset,
            CodeHash = HashCode(plainCode),
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            ExpiresAt = DateTime.UtcNow.AddMinutes(10)
        });
        await context.SaveChangesAsync();

        var service = CreateAuthService(context, emailService);

        var result = await service.ConfirmPasswordResetAsync(new PasswordResetConfirmRequest
        {
            Email = user.Email,
            Code = plainCode,
            NewPassword = "new-password"
        });

        var storedCode = context.EmailVerificationCodes.Single();
        var storedUser = context.Users.Single();

        Assert.True(result.IsSuccess);
        Assert.True(BCrypt.Net.BCrypt.Verify("new-password", storedUser.PasswordHash));
        Assert.NotNull(storedCode.UsedAt);
    }

    private static AuthService CreateAuthService(FlowMeet.Server.Data.AppDbContext context, TestEmailService emailService)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "super-secret-key-super-secret-key",
                ["Jwt:Issuer"] = "FlowMeet",
                ["Jwt:Audience"] = "FlowMeetClient"
            })
            .Build();

        return new AuthService(
            context,
            configuration,
            emailService,
            Options.Create(new SmtpEmailOptions()));
    }

    private static string HashCode(string code)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(code));
        return Convert.ToHexString(bytes);
    }
}
