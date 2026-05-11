using System.Text.RegularExpressions;
using FlowMeet.Server.Data;
using FlowMeet.Server.Models.DTOs;
using FlowMeet.Server.Models.Entities;
using FlowMeet.Server.Services;

namespace FlowMeet.Server.Tests;

public class ProfileServiceTests
{
    [Fact]
    public async Task UpdateProfileAsync_UpdatesFieldsAndSyncsMeetingNotifications()
    {
        await using var context = TestDbFactory.CreateContext();
        var notifications = new RecordingNotificationService();
        var service = CreateService(context, notificationService: notifications);
        var userId = Guid.NewGuid();
        const string settingsJson = "{\"theme\":\"sunrise\"}";

        context.Users.Add(CreateUser(userId, firstName: "Old", lastName: "Name", settingsJson: "{}"));
        await context.SaveChangesAsync();

        var result = await service.UpdateProfileAsync(userId, new UpdateProfileRequest
        {
            FirstName = "New",
            SettingsJson = settingsJson
        });

        var storedUser = await context.Users.FindAsync(userId);

        Assert.True(result.IsSuccess);
        Assert.NotNull(storedUser);
        Assert.Equal("New", storedUser!.FirstName);
        Assert.Equal("Name", storedUser.LastName);
        Assert.Equal(settingsJson, storedUser.SettingsJson);
        Assert.Equal(userId, Assert.Single(notifications.SyncedMeetingNotificationUserIds));
    }

    [Fact]
    public async Task RequestEmailChangeAsync_ReplacesOldCodeAndSendsEmail()
    {
        await using var context = TestDbFactory.CreateContext();
        var emailService = new TestEmailService();
        var service = CreateService(context, emailService: emailService);
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        context.Users.Add(CreateUser(userId, email: "current@example.com"));
        context.EmailVerificationCodes.Add(new EmailVerificationCode
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Email = "old@example.com",
            Purpose = EmailVerificationPurpose.EmailChange,
            CodeHash = "OLDHASH",
            CreatedAt = now.AddMinutes(-2),
            ExpiresAt = now.AddMinutes(10)
        });
        await context.SaveChangesAsync();

        var result = await service.RequestEmailChangeAsync(userId, "new@example.com");
        var storedCodes = context.EmailVerificationCodes
            .Where(code => code.UserId == userId && code.Purpose == EmailVerificationPurpose.EmailChange)
            .ToList();

        Assert.True(result.IsSuccess);
        var storedCode = Assert.Single(storedCodes);
        Assert.Equal("new@example.com", storedCode.Email);
        Assert.True(storedCode.ExpiresAt > storedCode.CreatedAt);
        Assert.NotEqual("OLDHASH", storedCode.CodeHash);
        Assert.Single(emailService.SentEmails);
        Assert.Equal("new@example.com", emailService.SentEmails[0].ToEmail);
        Assert.Matches(@"\b\d{6}\b", emailService.SentEmails[0].TextBody);
    }

    [Fact]
    public async Task ConfirmEmailChangeAsync_WithValidCode_UpdatesEmailAndMarksCodeUsed()
    {
        await using var context = TestDbFactory.CreateContext();
        var emailService = new TestEmailService();
        var service = CreateService(context, emailService: emailService);
        var userId = Guid.NewGuid();
        const string newEmail = "updated@example.com";

        context.Users.Add(CreateUser(userId, email: "current@example.com"));
        await context.SaveChangesAsync();

        var requestResult = await service.RequestEmailChangeAsync(userId, newEmail);
        var code = ExtractVerificationCode(emailService.SentEmails[0].TextBody);

        var confirmResult = await service.ConfirmEmailChangeAsync(userId, new ConfirmEmailChangeRequest
        {
            NewEmail = newEmail,
            Code = code
        });

        var storedUser = await context.Users.FindAsync(userId);
        var verificationCode = Assert.Single(context.EmailVerificationCodes);

        Assert.True(requestResult.IsSuccess);
        Assert.True(confirmResult.IsSuccess);
        Assert.NotNull(storedUser);
        Assert.Equal(newEmail, storedUser!.Email);
        Assert.Equal(newEmail, confirmResult.Profile!.Email);
        Assert.NotNull(verificationCode.UsedAt);
    }

    [Fact]
    public async Task ConfirmEmailChangeAsync_WithInvalidCode_ReturnsError()
    {
        await using var context = TestDbFactory.CreateContext();
        var emailService = new TestEmailService();
        var service = CreateService(context, emailService: emailService);
        var userId = Guid.NewGuid();
        const string newEmail = "updated@example.com";

        context.Users.Add(CreateUser(userId, email: "current@example.com"));
        await context.SaveChangesAsync();

        await service.RequestEmailChangeAsync(userId, newEmail);

        var result = await service.ConfirmEmailChangeAsync(userId, new ConfirmEmailChangeRequest
        {
            NewEmail = newEmail,
            Code = "000000"
        });

        var storedUser = await context.Users.FindAsync(userId);

        Assert.False(result.IsSuccess);
        Assert.Equal("Код подтверждения недействителен или истёк", result.ErrorMessage);
        Assert.NotNull(storedUser);
        Assert.Equal("current@example.com", storedUser!.Email);
    }

    [Fact]
    public async Task ChangePasswordAsync_UpdatesHashWhenCurrentPasswordIsValid()
    {
        await using var context = TestDbFactory.CreateContext();
        var service = CreateService(context);
        var userId = Guid.NewGuid();
        const string currentPassword = "old-secret";
        const string newPassword = "new-secret";

        context.Users.Add(CreateUser(
            userId,
            passwordHash: BCrypt.Net.BCrypt.HashPassword(currentPassword)));
        await context.SaveChangesAsync();

        var result = await service.ChangePasswordAsync(userId, new ChangePasswordRequest
        {
            CurrentPassword = currentPassword,
            NewPassword = newPassword
        });

        var storedUser = await context.Users.FindAsync(userId);

        Assert.True(result.IsSuccess);
        Assert.NotNull(storedUser);
        Assert.True(BCrypt.Net.BCrypt.Verify(newPassword, storedUser!.PasswordHash));
        Assert.False(BCrypt.Net.BCrypt.Verify(currentPassword, storedUser.PasswordHash));
    }

    [Fact]
    public async Task ChangePasswordAsync_ReturnsErrorWhenCurrentPasswordIsWrong()
    {
        await using var context = TestDbFactory.CreateContext();
        var service = CreateService(context);
        var userId = Guid.NewGuid();
        const string currentPassword = "old-secret";
        var originalHash = BCrypt.Net.BCrypt.HashPassword(currentPassword);

        context.Users.Add(CreateUser(userId, passwordHash: originalHash));
        await context.SaveChangesAsync();

        var result = await service.ChangePasswordAsync(userId, new ChangePasswordRequest
        {
            CurrentPassword = "wrong-password",
            NewPassword = "new-secret"
        });

        var storedUser = await context.Users.FindAsync(userId);

        Assert.False(result.IsSuccess);
        Assert.Equal("Текущий пароль указан неверно", result.ErrorMessage);
        Assert.NotNull(storedUser);
        Assert.True(BCrypt.Net.BCrypt.Verify(currentPassword, storedUser!.PasswordHash));
    }

    [Fact]
    public async Task GetBaseScheduleHistoryAndExceptionsAsync_ReturnOrderedData()
    {
        await using var context = TestDbFactory.CreateContext();
        var service = CreateService(context);
        var userId = Guid.NewGuid();
        var entryOneId = Guid.NewGuid();
        var entryTwoId = Guid.NewGuid();

        context.Users.Add(CreateUser(userId));
        context.BaseScheduleEntries.AddRange(
            new BaseScheduleEntry
            {
                Id = entryOneId,
                UserId = userId,
                DayOfWeek = 3,
                Title = "Late class",
                StartTime = TimeSpan.FromHours(14),
                EndTime = TimeSpan.FromHours(15),
                Type = EventType.Mandatory,
                EffectiveFromDate = new DateOnly(2026, 5, 1),
                EffectiveToDate = new DateOnly(2026, 5, 20)
            },
            new BaseScheduleEntry
            {
                Id = entryTwoId,
                UserId = userId,
                DayOfWeek = 1,
                Title = "Early class",
                StartTime = TimeSpan.FromHours(9),
                EndTime = TimeSpan.FromHours(10),
                Type = EventType.Flexible,
                EffectiveFromDate = new DateOnly(2026, 5, 5)
            });
        context.BaseScheduleOccurrenceExceptions.AddRange(
            new BaseScheduleOccurrenceException
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                BaseScheduleEntryId = entryTwoId,
                Date = new DateOnly(2026, 5, 9)
            },
            new BaseScheduleOccurrenceException
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                BaseScheduleEntryId = entryOneId,
                Date = new DateOnly(2026, 5, 7)
            });
        await context.SaveChangesAsync();

        var history = await service.GetBaseScheduleHistoryAsync(userId, new DateOnly(2026, 5, 6), new DateOnly(2026, 5, 10));
        var exceptions = await service.GetBaseScheduleExceptionsAsync(userId);

        Assert.Equal(2, history.Count);
        Assert.Equal(new[] { "Early class", "Late class" }, history.Select(entry => entry.Title).ToArray());
        Assert.Equal(new[] { new DateOnly(2026, 5, 7), new DateOnly(2026, 5, 9) }, exceptions.Select(item => item.Date).ToArray());
    }

    [Fact]
    public async Task UpdateBaseScheduleAsync_EditsCreateNewVersionAndPreserveOldOne()
    {
        await using var context = TestDbFactory.CreateContext();
        var service = CreateService(context);
        var userId = Guid.NewGuid();
        var originalEntryId = Guid.NewGuid();

        context.Users.Add(CreateUser(userId));
        context.BaseScheduleEntries.Add(new BaseScheduleEntry
        {
            Id = originalEntryId,
            UserId = userId,
            DayOfWeek = 1,
            Title = "Старая пара",
            StartTime = TimeSpan.FromHours(10),
            EndTime = TimeSpan.FromHours(11),
            Type = EventType.Mandatory,
            EffectiveFromDate = new DateOnly(1, 1, 1)
        });
        await context.SaveChangesAsync();

        var result = await service.UpdateBaseScheduleAsync(userId, new List<BaseScheduleEntryDto>
        {
            new()
            {
                Id = originalEntryId,
                DayOfWeek = 1,
                Title = "Новая пара",
                StartTime = "10:00",
                EndTime = "11:30",
                Type = EventType.Flexible
            }
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(2, context.BaseScheduleEntries.Count());

        var archivedEntry = context.BaseScheduleEntries.Single(entry => entry.Id == originalEntryId);
        var activeEntry = context.BaseScheduleEntries.Single(entry => entry.Id != originalEntryId);

        Assert.NotNull(archivedEntry.EffectiveToDate);
        Assert.Equal("Новая пара", activeEntry.Title);
        Assert.Null(activeEntry.EffectiveToDate);
        Assert.Single(result.Schedule!);
        Assert.Equal(activeEntry.Id, result.Schedule!.Single().Id);
    }

    [Fact]
    public async Task UpdateBaseScheduleAsync_ReassignsFutureExceptionsToNewVersionOnly()
    {
        await using var context = TestDbFactory.CreateContext();
        var service = CreateService(context);
        var userId = Guid.NewGuid();
        var originalEntryId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        context.Users.Add(CreateUser(userId));
        context.BaseScheduleEntries.Add(new BaseScheduleEntry
        {
            Id = originalEntryId,
            UserId = userId,
            DayOfWeek = 2,
            Title = "Математика",
            StartTime = TimeSpan.FromHours(10),
            EndTime = TimeSpan.FromHours(11),
            Type = EventType.Mandatory,
            EffectiveFromDate = new DateOnly(1, 1, 1)
        });

        var pastException = new BaseScheduleOccurrenceException
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            BaseScheduleEntryId = originalEntryId,
            Date = today.AddDays(-1)
        };
        var futureException = new BaseScheduleOccurrenceException
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            BaseScheduleEntryId = originalEntryId,
            Date = today.AddDays(2)
        };

        context.BaseScheduleOccurrenceExceptions.AddRange(pastException, futureException);
        await context.SaveChangesAsync();

        var result = await service.UpdateBaseScheduleAsync(userId, new List<BaseScheduleEntryDto>
        {
            new()
            {
                Id = originalEntryId,
                DayOfWeek = 2,
                Title = "Физика",
                StartTime = "10:30",
                EndTime = "11:30",
                Type = EventType.Flexible
            }
        });

        var activeEntry = context.BaseScheduleEntries.Single(entry => entry.Id != originalEntryId);
        var storedPastException = await context.BaseScheduleOccurrenceExceptions.FindAsync(pastException.Id);
        var storedFutureException = await context.BaseScheduleOccurrenceExceptions.FindAsync(futureException.Id);

        Assert.True(result.IsSuccess);
        Assert.NotNull(storedPastException);
        Assert.NotNull(storedFutureException);
        Assert.Equal(originalEntryId, storedPastException!.BaseScheduleEntryId);
        Assert.Equal(activeEntry.Id, storedFutureException!.BaseScheduleEntryId);
    }

    private static ProfileService CreateService(
        AppDbContext context,
        TestEmailService? emailService = null,
        RecordingNotificationService? notificationService = null)
        => new(
            context,
            emailService ?? new TestEmailService(),
            notificationService ?? new RecordingNotificationService());

    private static User CreateUser(
        Guid userId,
        string? email = null,
        string firstName = "Test",
        string lastName = "User",
        string? passwordHash = null,
        string settingsJson = "{}") => new()
    {
        Id = userId,
        Email = email ?? $"{userId:N}@example.com",
        PasswordHash = passwordHash ?? "hash",
        FirstName = firstName,
        LastName = lastName,
        SettingsJson = settingsJson
    };

    private static string ExtractVerificationCode(string emailBody)
    {
        var match = Regex.Match(emailBody, @"\b\d{6}\b");
        Assert.True(match.Success);
        return match.Value;
    }
}
