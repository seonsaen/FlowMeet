using FlowMeet.Server.Data;
using FlowMeet.Server.Models.DTOs;
using FlowMeet.Server.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace FlowMeet.Server.Services;

public class ProfileService : IProfileService
{
    private readonly AppDbContext _context;

    public ProfileService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<(bool IsSuccess, string ErrorMessage, ProfileResponse? Profile)> GetProfileAsync(Guid userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return (false, "Пользователь не найден", null);

        return (true, string.Empty, new ProfileResponse
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            AvatarUrl = user.AvatarUrl,
            SettingsJson = user.SettingsJson
        });
    }

    public async Task<(bool IsSuccess, string ErrorMessage, ProfileResponse? Profile)> UpdateProfileAsync(Guid userId, UpdateProfileRequest request)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return (false, "Пользователь не найден", null);

        if (!string.IsNullOrWhiteSpace(request.Email) && request.Email != user.Email)
        {
            var emailTaken = await _context.Users.AnyAsync(u => u.Email == request.Email && u.Id != userId);
            if (emailTaken)
                return (false, "Пользователь с таким email уже существует", null);

            user.Email = request.Email;
        }

        if (!string.IsNullOrWhiteSpace(request.FirstName)) user.FirstName = request.FirstName;
        if (!string.IsNullOrWhiteSpace(request.LastName)) user.LastName = request.LastName;
        if (request.AvatarUrl != null) user.AvatarUrl = request.AvatarUrl;
        if (request.SettingsJson != null) user.SettingsJson = request.SettingsJson;

        if (!string.IsNullOrWhiteSpace(request.NewPassword))
        {
            if (string.IsNullOrWhiteSpace(request.CurrentPassword))
                return (false, "Для смены пароля укажите текущий пароль", null);

            if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
                return (false, "Текущий пароль указан неверно", null);

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        }

        await _context.SaveChangesAsync();

        return (true, string.Empty, new ProfileResponse
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            AvatarUrl = user.AvatarUrl,
            SettingsJson = user.SettingsJson
        });
    }

    public async Task<List<BaseScheduleEntryDto>> GetBaseScheduleAsync(Guid userId)
    {
        var entries = await _context.BaseScheduleEntries
            .Where(e => e.UserId == userId)
            .OrderBy(e => e.DayOfWeek)
            .ThenBy(e => e.StartTime)
            .ToListAsync();

        return entries.Select(e => new BaseScheduleEntryDto
        {
            Id = e.Id,
            DayOfWeek = e.DayOfWeek,
            Title = e.Title,
            Description = e.Description,
            StartTime = e.StartTime.ToString(@"hh\:mm"),
            EndTime = e.EndTime.ToString(@"hh\:mm"),
            Type = e.Type
        }).ToList();
    }

    public async Task<(bool IsSuccess, string ErrorMessage, List<BaseScheduleEntryDto>? Schedule)> UpdateBaseScheduleAsync(Guid userId, List<BaseScheduleEntryDto> entries)
    {
        var userExists = await _context.Users.AnyAsync(u => u.Id == userId);
        if (!userExists) return (false, "Пользователь не найден", null);

        // Безопасный парсинг времени
        var newEntries = new List<BaseScheduleEntry>();
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

            newEntries.Add(new BaseScheduleEntry
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                DayOfWeek = dto.DayOfWeek,
                Title = dto.Title,
                Description = dto.Description,
                StartTime = startTime,
                EndTime = endTime,
                Type = dto.Type
            });
        }

        // Транзакционное обновление: удаляем старое, сохраняем новое
        var old = _context.BaseScheduleEntries.Where(e => e.UserId == userId);
        _context.BaseScheduleEntries.RemoveRange(old);
        
        _context.BaseScheduleEntries.AddRange(newEntries);
        await _context.SaveChangesAsync();

        // Возвращаем обновленный список
        var result = newEntries.Select(e => new BaseScheduleEntryDto
        {
            Id = e.Id,
            DayOfWeek = e.DayOfWeek,
            Title = e.Title,
            Description = e.Description,
            StartTime = e.StartTime.ToString(@"hh\:mm"),
            EndTime = e.EndTime.ToString(@"hh\:mm"),
            Type = e.Type
        }).ToList();

        return (true, string.Empty, result);
    }
}
