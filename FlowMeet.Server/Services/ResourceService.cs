using FlowMeet.Server.Data;
using FlowMeet.Server.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace FlowMeet.Server.Services;

public class ResourceService
{
    private readonly AppDbContext _context;

    public ResourceService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<int> CalculateResourceAsync(Guid userId)
    {
        // Получаем последнее настроение за сегодня
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        
        var userState = await _context.UserStates
            .Where(us => us.UserId == userId && us.Date == today)
            .FirstOrDefaultAsync();

        int mood = userState?.MoodLevel ?? 3; // Если не отметил, считаем среднее 3

        // Считаем нагрузку по расписанию на сегодня
        // Ищем события, которые пересекаются с сегодняшним днем
        var startOfDay = DateTime.UtcNow.Date;
        var endOfDay = startOfDay.AddDays(1);

        var events = await _context.Events
            .Where(e => e.UserId == userId 
                        && e.StartTime < endOfDay 
                        && e.EndTime > startOfDay)
            .ToListAsync();

        double workHours = 0;
        
        foreach (var ev in events)
        {
            // Считаем длительность события в часах
            var duration = (ev.EndTime - ev.StartTime).TotalHours;

            // Обязательные дела отнимают больше ресурса
            if (ev.Type == EventType.Mandatory)
            {
                workHours += duration * 1.5; // Коэффициент усталости 1.5
            }
            else
            {
                workHours += duration * 0.5; // Гибкие дела утомляют меньше
            }
        }
        
        int baseResource = mood * 20;
        int resource = baseResource - (int)(workHours * 10);
        
        if (resource > 100) resource = 100;
        if (resource < 0) resource = 0;

        return resource;
    }
}