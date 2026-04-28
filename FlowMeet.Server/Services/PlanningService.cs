using FlowMeet.Server.Data;
using FlowMeet.Server.Models.DTOs;
using FlowMeet.Server.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace FlowMeet.Server.Services;

public class PlanningService : IPlanningService
{
    private readonly AppDbContext _context;
    private readonly IUserStateService _userStateService;

    public PlanningService(AppDbContext context, IUserStateService userStateService)
    {
        _context = context;
        _userStateService = userStateService;
    }
    
        

    public async Task<(bool IsSuccess, string ErrorMessage, List<TimeSlotDto> Slots)> FindGroupSlotsAsync(Guid currentUserId, List<Guid> participantIds, DateOnly startDate, int durationMinutes)
    {
        if (durationMinutes <= 0)
            return (false, "Длительность встречи должна быть положительной", new List<TimeSlotDto>());
        
        var distinctParticipantIds = participantIds
            .Append(currentUserId)
            .Distinct()
            .ToList();

        var existingUserIds = await _context.Users
            .Where(u => distinctParticipantIds.Contains(u.Id))
            .Select(u => u.Id)
            .ToListAsync();

        var missingUserIds = distinctParticipantIds.Except(existingUserIds).ToList();
        if (missingUserIds.Any())
            return (false, "Один или несколько участников не найдены", new List<TimeSlotDto>());

        var otherParticipantIds = distinctParticipantIds
            .Where(id => id != currentUserId)
            .ToList();

        if (otherParticipantIds.Any())
        {
            var friendIds = await _context.Friendships
                .Where(f => f.Status == FriendshipStatus.Accepted
                            && (f.RequesterId == currentUserId || f.AddresseeId == currentUserId))
                .Select(f => f.RequesterId == currentUserId ? f.AddresseeId : f.RequesterId)
                .ToListAsync();

            var currentUserGroupIds = await _context.GroupMembers
                .Where(gm => gm.UserId == currentUserId)
                .Select(gm => gm.GroupId)
                .ToListAsync();

            var sharedGroupUserIds = await _context.GroupMembers
                .Where(gm => currentUserGroupIds.Contains(gm.GroupId))
                .Select(gm => gm.UserId)
                .Distinct()
                .ToListAsync();

            var allowedUserIds = friendIds
                .Concat(sharedGroupUserIds)
                .Distinct()
                .ToHashSet();

            var unauthorizedUserIds = otherParticipantIds
                .Where(id => !allowedUserIds.Contains(id))
                .ToList();

            if (unauthorizedUserIds.Any())
                return (false, "Можно планировать только с друзьями или участниками общих групп", new List<TimeSlotDto>());
        }

        var result = new List<TimeSlotDto>();
        
        // Собираем данные о ресурсе всех участников
        var resources = new Dictionary<Guid, int>();
        foreach (var userId in distinctParticipantIds)
        {
            var resourceStatus = await _userStateService.GetResourceStatusAsync(userId);
            resources[userId] = resourceStatus.ResourceLevel;
        }

        // Загружаем события всех участников на неделю вперед
        var startRange = startDate.ToDateTime(new TimeOnly(0, 0)).ToUniversalTime();
        var endRange = startRange.AddDays(7); // Ищем в пределах недели

        var allEvents = await _context.Events
            .Where(e => distinctParticipantIds.Contains(e.UserId)
                        && e.StartTime < endRange
                        && e.EndTime > startRange)
            .ToListAsync();

        var baseScheduleEntries = await _context.BaseScheduleEntries
            .Where(e => distinctParticipantIds.Contains(e.UserId))
            .ToListAsync();
        
        for (int day = 0; day < 7; day++)
        {
            var currentDayDate = startDate.AddDays(day);
            var currentDayOfWeek = (int)currentDayDate.DayOfWeek;

            var dayBaseEvents = baseScheduleEntries
                .Where(e => e.DayOfWeek == currentDayOfWeek)
                .Select(e => new Event
                {
                    Id = Guid.NewGuid(),
                    UserId = e.UserId,
                    Title = e.Title,
                    Description = e.Description,
                    StartTime = currentDayDate.ToDateTime(TimeOnly.FromTimeSpan(e.StartTime)).ToUniversalTime(),
                    EndTime = currentDayDate.ToDateTime(TimeOnly.FromTimeSpan(e.EndTime)).ToUniversalTime(),
                    Type = e.Type
                })
                .ToList();

            var dayEvents = allEvents
                .Where(e => e.StartTime < currentDayDate.ToDateTime(new TimeOnly(23, 59)).ToUniversalTime().AddMinutes(1)
                            && e.EndTime > currentDayDate.ToDateTime(new TimeOnly(0, 0)).ToUniversalTime())
                .ToList();

            var combinedEvents = dayEvents
                .Concat(dayBaseEvents)
                .ToList();
            
            var cursor = currentDayDate.ToDateTime(new TimeOnly(9, 0)).ToUniversalTime();
            var limit = currentDayDate.ToDateTime(new TimeOnly(22, 0)).ToUniversalTime();

            while (cursor.AddMinutes(durationMinutes) <= limit)
            {
                var slotStart = cursor;
                var slotEnd = cursor.AddMinutes(durationMinutes);

                var conflicts = combinedEvents
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
        return (true, string.Empty, result);
    }
}
