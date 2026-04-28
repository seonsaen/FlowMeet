using FlowMeet.Server.Data;
using FlowMeet.Server.Models.DTOs;
using FlowMeet.Server.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace FlowMeet.Server.Services;

public class UserStateService : IUserStateService
{
    private readonly AppDbContext _context;

    public UserStateService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<(int ResourceLevel, string Message)> SetMoodAsync(Guid userId, MoodRequest request)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var state = await _context.UserStates
            .FirstOrDefaultAsync(us => us.UserId == userId && us.Date == today);

        if (state == null)
        {
            state = new UserState
            {
                UserId = userId,
                Date = today,
                MoodLevel = request.MoodLevel,
                ResourceLevel = 100 // Временное значение до расчета
            };
            _context.UserStates.Add(state);
        }
        else
        {
            state.MoodLevel = request.MoodLevel;
        }

        // Сохраняем настроение, чтобы оно попало в базу
        await _context.SaveChangesAsync();
        
        // Считаем новый ресурс на основе обновленного настроения
        var currentResource = await CalculateResourceInternalAsync(userId, today);
        
        // Обновляем вычисленный ресурс в бд
        state.ResourceLevel = currentResource;
        await _context.SaveChangesAsync();

        return (currentResource, "Настроение сохранено");
    }

    public async Task<ResourceResponse> GetResourceStatusAsync(Guid userId)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var level = await CalculateResourceInternalAsync(userId, today);
        
        string msg = level switch
        {
            > 80 => "Вы полны энергии, отличное время для встреч",
            > 50 => "Нормальный уровень, можно идти на встречу",
            > 20 => "Вы устали, лучше выбрать короткие встречи",
            _ => "Вы очень сильно устали, рекомендуется отдых"
        };

        return new ResourceResponse { ResourceLevel = level, StatusMessage = msg };
    }

    // Приватный метод для внутренних расчетов
    private async Task<int> CalculateResourceInternalAsync(Guid userId, DateOnly date)
    {
        var userState = await _context.UserStates
            .Where(us => us.UserId == userId && us.Date == date)
            .FirstOrDefaultAsync();

        int mood = userState?.MoodLevel ?? 3;

        var startOfDay = date.ToDateTime(new TimeOnly(0, 0)).ToUniversalTime();
        var endOfDay = startOfDay.AddDays(1);

        var events = await _context.Events
            .Where(e => e.UserId == userId 
                        && e.StartTime < endOfDay 
                        && e.EndTime > startOfDay)
            .ToListAsync();

        double workHours = 0;
        
        foreach (var ev in events)
        {
            var duration = (ev.EndTime - ev.StartTime).TotalHours;

            if (ev.Type == EventType.Mandatory)
            {
                workHours += duration * 1.5;
            }
            else
            {
                workHours += duration * 0.5;
            }
        }
        
        int baseResource = mood * 20;
        int resource = baseResource - (int)(workHours * 10);
        
        if (resource > 100) resource = 100;
        if (resource < 0) resource = 0;

        return resource;
    }
}