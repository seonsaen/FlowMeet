using FlowMeet.Server.Data;
using FlowMeet.Server.Models.DTOs;
using FlowMeet.Server.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace FlowMeet.Server.Services;

public class PlanningService
{
    private readonly AppDbContext _context;
    private readonly ResourceService _resourceService;

    public PlanningService(AppDbContext context, ResourceService resourceService)
    {
        _context = context;
        _resourceService = resourceService;
    }

    public async Task<List<TimeSlotDto>> FindGroupSlotsAsync(List<Guid> participantIds, DateOnly startDate, int durationMinutes)
    {
        var result = new List<TimeSlotDto>();

        // Собираем данные о ресурсе всех участников
        var resources = new Dictionary<Guid, int>();
        foreach (var userId in participantIds)
        {
            resources[userId] = await _resourceService.CalculateResourceAsync(userId);
        }

        // Загружаем события всех участников на неделю вперед
        var startRange = startDate.ToDateTime(new TimeOnly(0, 0)).ToUniversalTime();
        var endRange = startRange.AddDays(7); // Ищем в пределах недели

        var allEvents = await _context.Events
            .Where(e => participantIds.Contains(e.UserId) 
                        && e.StartTime < endRange && e.EndTime > startRange)
            .ToListAsync();
        
        for (int day = 0; day < 7; day++)
        {
            var currentDayDate = startDate.AddDays(day);
            
            var cursor = currentDayDate.ToDateTime(new TimeOnly(9, 0)).ToUniversalTime();
            var limit = currentDayDate.ToDateTime(new TimeOnly(22, 0)).ToUniversalTime();
            
            while (cursor.AddMinutes(durationMinutes) <= limit)
            {
                var slotStart = cursor;
                var slotEnd = cursor.AddMinutes(durationMinutes);
                
                var conflicts = allEvents
                    .Where(e => e.StartTime < slotEnd && e.EndTime > slotStart)
                    .ToList();

                // Проверяем, есть ли конфликты
                bool hasMandatoryConflict = conflicts.Any(c => c.Type == EventType.Mandatory);

                // Слот свободен или занят только гибкими делами
                if (!hasMandatoryConflict)
                {
                    // Если хотя бы у одного участника ресурс < 30, считаем слот "тяжелым"
                    bool lowEnergy = resources.Values.Any(r => r < 30);
                    
                    // Нужно ли двигать гибкие дела?
                    bool requiresMoving = conflicts.Any(); // Если список конфликтов не пуст, значит надо двигать

                    string suitability = "Optimal";
                    string description = "Идеальное время для всех";

                    if (requiresMoving)
                    {
                        suitability = "RequiresMoving";
                        description = "Потребуется перенос гибких дел";
                    }
                    else if (lowEnergy)
                    {
                        suitability = "LowEnergy";
                        description = "Время свободное, но кто-то из группы устал";
                    }

                    result.Add(new TimeSlotDto
                    {
                        StartTime = slotStart,
                        EndTime = slotEnd,
                        Suitability = suitability,
                        Description = description
                    });
                }

                cursor = cursor.AddMinutes(30);
            }
        }

        return result;
    }
}