using FlowMeet.Server.Data;
using FlowMeet.Server.Models.DTOs;
using FlowMeet.Server.Models.Entities;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace FlowMeet.Server.Services;

public class ProfileService : IProfileService
{
    private readonly AppDbContext _context;
    private readonly IEmailService _emailService;
    private readonly INotificationService _notificationService;
    private static DateOnly TodayUtc => DateOnly.FromDateTime(DateTime.UtcNow);

    public ProfileService(
        AppDbContext context,
        IEmailService emailService,
        INotificationService notificationService)
    {
        _context = context;
        _emailService = emailService;
        _notificationService = notificationService;
    }

    public async Task<(bool IsSuccess, string ErrorMessage, ProfileResponse? Profile)> GetProfileAsync(Guid userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return (false, "Пользователь не найден", null);

        return (true, string.Empty, ToProfileResponse(user));
    }

    public async Task<(bool IsSuccess, string ErrorMessage, ProfileResponse? Profile)> UpdateProfileAsync(Guid userId, UpdateProfileRequest request)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return (false, "Пользователь не найден", null);

        if (!string.IsNullOrWhiteSpace(request.FirstName)) user.FirstName = request.FirstName;
        if (!string.IsNullOrWhiteSpace(request.LastName)) user.LastName = request.LastName;
        if (request.SettingsJson != null) user.SettingsJson = request.SettingsJson;

        await _context.SaveChangesAsync();
        await _notificationService.SyncMeetingNotificationsForUserAsync(userId);

        return (true, string.Empty, ToProfileResponse(user));
    }

    public async Task<(bool IsSuccess, string ErrorMessage)> RequestEmailChangeAsync(Guid userId, string newEmail, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
            return (false, "Пользователь не найден");

        if (string.Equals(user.Email, newEmail, StringComparison.OrdinalIgnoreCase))
            return (false, "Укажите новый email, отличный от текущего");

        var emailTaken = await _context.Users.AnyAsync(existingUser => existingUser.Email == newEmail && existingUser.Id != userId, cancellationToken);
        if (emailTaken)
            return (false, "Пользователь с таким email уже существует");

        var verificationCode = GenerateVerificationCode();
        await UpsertEmailVerificationCodeAsync(userId, newEmail, verificationCode, cancellationToken);
        await SendVerificationCodeAsync(newEmail, verificationCode, cancellationToken);

        return (true, string.Empty);
    }

    public async Task<(bool IsSuccess, string ErrorMessage, ProfileResponse? Profile)> ConfirmEmailChangeAsync(Guid userId, ConfirmEmailChangeRequest request)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
            return (false, "Пользователь не найден", null);

        var verificationCode = await _context.EmailVerificationCodes
            .Where(code => code.UserId == userId
                           && code.Email == request.NewEmail
                           && code.Purpose == EmailVerificationPurpose.EmailChange
                           && code.UsedAt == null
                           && code.ExpiresAt >= DateTime.UtcNow)
            .OrderByDescending(code => code.CreatedAt)
            .FirstOrDefaultAsync(code => code.CodeHash == HashCode(request.Code));

        if (verificationCode == null)
            return (false, "Код подтверждения недействителен или истёк", null);

        var emailTaken = await _context.Users.AnyAsync(existingUser => existingUser.Email == request.NewEmail && existingUser.Id != userId);
        if (emailTaken)
            return (false, "Пользователь с таким email уже существует", null);

        user.Email = request.NewEmail;
        verificationCode.UsedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return (true, string.Empty, ToProfileResponse(user));
    }

    public async Task<(bool IsSuccess, string ErrorMessage)> ChangePasswordAsync(Guid userId, ChangePasswordRequest request)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
            return (false, "Пользователь не найден");

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            return (false, "Текущий пароль указан неверно");

        if (request.CurrentPassword == request.NewPassword)
            return (false, "Новый пароль должен отличаться от текущего");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await _context.SaveChangesAsync();

        return (true, string.Empty);
    }

    public async Task<List<BaseScheduleEntryDto>> GetBaseScheduleAsync(Guid userId)
    {
        var today = TodayUtc;
        var entries = await _context.BaseScheduleEntries
            .Where(e => e.UserId == userId
                        && e.EffectiveFromDate <= today
                        && (e.EffectiveToDate == null || e.EffectiveToDate > today))
            .OrderBy(e => e.DayOfWeek)
            .ThenBy(e => e.StartTime)
            .ToListAsync();

        return entries.Select(ToBaseScheduleDto).ToList();
    }

    public async Task<List<BaseScheduleEntryDto>> GetBaseScheduleHistoryAsync(Guid userId, DateOnly fromDate, DateOnly toDate)
    {
        var entries = await _context.BaseScheduleEntries
            .AsNoTracking()
            .Where(entry => entry.UserId == userId
                            && entry.EffectiveFromDate <= toDate
                            && (entry.EffectiveToDate == null || entry.EffectiveToDate > fromDate))
            .OrderBy(entry => entry.DayOfWeek)
            .ThenBy(entry => entry.StartTime)
            .ToListAsync();

        return entries.Select(ToBaseScheduleDto).ToList();
    }

    public async Task<List<BaseScheduleOccurrenceExceptionDto>> GetBaseScheduleExceptionsAsync(Guid userId)
    {
        return await _context.BaseScheduleOccurrenceExceptions
            .AsNoTracking()
            .Where(exception => exception.UserId == userId)
            .OrderBy(exception => exception.Date)
            .Select(exception => new BaseScheduleOccurrenceExceptionDto
            {
                Id = exception.Id,
                BaseScheduleEntryId = exception.BaseScheduleEntryId,
                Date = exception.Date,
                OverrideEventId = exception.OverrideEventId
            })
            .ToListAsync();
    }

    public async Task<(bool IsSuccess, string ErrorMessage, List<BaseScheduleEntryDto>? Schedule)> UpdateBaseScheduleAsync(Guid userId, List<BaseScheduleEntryDto> entries)
    {
        var userExists = await _context.Users.AnyAsync(u => u.Id == userId);
        if (!userExists) return (false, "Пользователь не найден", null);

        var today = TodayUtc;
        var existingEntries = await _context.BaseScheduleEntries
            .Where(entry => entry.UserId == userId)
            .ToListAsync();

        var activeEntries = existingEntries
            .Where(entry => entry.EffectiveFromDate <= today && (entry.EffectiveToDate == null || entry.EffectiveToDate > today))
            .ToList();

        var activeById = activeEntries.ToDictionary(entry => entry.Id);
        var incomingIds = entries
            .Where(entry => entry.Id.HasValue)
            .Select(entry => entry.Id!.Value)
            .ToHashSet();

        var entriesToArchive = activeEntries
            .Where(entry => !incomingIds.Contains(entry.Id))
            .ToList();

        foreach (var dto in entries)
        {
            if (!TimeSpan.TryParse(dto.StartTime, out var startTime) || 
                !TimeSpan.TryParse(dto.EndTime, out var endTime))
            {
                return (false, $"Неверный формат времени для события: {dto.Title}", null);
            }

            if (endTime <= startTime)
            {
                return (false, $"Время окончания должно быть позже времени начала: {dto.Title}", null);
            }

            if (dto.Id.HasValue && activeById.TryGetValue(dto.Id.Value, out var currentEntry))
            {
                var hasChanges = currentEntry.DayOfWeek != dto.DayOfWeek
                                 || currentEntry.Title != dto.Title
                                 || currentEntry.Description != dto.Description
                                 || currentEntry.StartTime != startTime
                                 || currentEntry.EndTime != endTime
                                 || currentEntry.Type != dto.Type;

                if (!hasChanges)
                    continue;

                currentEntry.EffectiveToDate = today;

                var versionedEntry = new BaseScheduleEntry
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    DayOfWeek = dto.DayOfWeek,
                    Title = dto.Title,
                    Description = dto.Description,
                    StartTime = startTime,
                    EndTime = endTime,
                    Type = dto.Type,
                    EffectiveFromDate = today
                };

                _context.BaseScheduleEntries.Add(versionedEntry);

                var futureExceptions = await _context.BaseScheduleOccurrenceExceptions
                    .Where(exception => exception.BaseScheduleEntryId == currentEntry.Id && exception.Date >= today)
                    .ToListAsync();

                foreach (var occurrenceException in futureExceptions)
                    occurrenceException.BaseScheduleEntryId = versionedEntry.Id;
            }
            else
            {
                var targetEntry = new BaseScheduleEntry
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    DayOfWeek = dto.DayOfWeek,
                    Title = dto.Title,
                    Description = dto.Description,
                    StartTime = startTime,
                    EndTime = endTime,
                    Type = dto.Type,
                    EffectiveFromDate = today
                };
                _context.BaseScheduleEntries.Add(targetEntry);
            }
        }

        if (entriesToArchive.Count > 0)
        {
            foreach (var entry in entriesToArchive)
                entry.EffectiveToDate = today;
        }

        await _context.SaveChangesAsync();

        var result = await GetBaseScheduleAsync(userId);

        return (true, string.Empty, result);
    }

    private static BaseScheduleEntryDto ToBaseScheduleDto(BaseScheduleEntry entry) => new()
    {
        Id = entry.Id,
        DayOfWeek = entry.DayOfWeek,
        Title = entry.Title,
        Description = entry.Description,
        StartTime = entry.StartTime.ToString(@"hh\:mm"),
        EndTime = entry.EndTime.ToString(@"hh\:mm"),
        Type = entry.Type,
        EffectiveFromDate = entry.EffectiveFromDate,
        EffectiveToDate = entry.EffectiveToDate
    };

    private static ProfileResponse ToProfileResponse(User user) => new()
    {
        Id = user.Id,
        Email = user.Email,
        FirstName = user.FirstName,
        LastName = user.LastName,
        SettingsJson = user.SettingsJson
    };

    private async Task UpsertEmailVerificationCodeAsync(Guid userId, string newEmail, string rawCode, CancellationToken cancellationToken)
    {
        var existingCodes = await _context.EmailVerificationCodes
            .Where(code => code.UserId == userId
                           && code.Purpose == EmailVerificationPurpose.EmailChange
                           && code.UsedAt == null)
            .ToListAsync(cancellationToken);

        if (existingCodes.Count > 0)
            _context.EmailVerificationCodes.RemoveRange(existingCodes);

        _context.EmailVerificationCodes.Add(new EmailVerificationCode
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Email = newEmail,
            Purpose = EmailVerificationPurpose.EmailChange,
            CodeHash = HashCode(rawCode),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15)
        });

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task SendVerificationCodeAsync(string email, string code, CancellationToken cancellationToken)
    {
        await _emailService.SendAsync(
            email,
            "Подтверждение email в FlowMeet",
            $"""
            Подтверждение email в FlowMeet

            Код подтверждения для смены email: {code}

            Код действителен 15 минут.
            """,
            cancellationToken);
    }

    private static string GenerateVerificationCode() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    private static string HashCode(string code)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(code));
        return Convert.ToHexString(bytes);
    }
}
