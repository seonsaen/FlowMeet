using FlowMeet.Server.Data;
using FlowMeet.Server.Models.Entities;
using FlowMeet.Server.Models.DTOs;
using FlowMeet.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

namespace FlowMeet.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UserStateController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ResourceService _resourceService;
    
    public UserStateController(AppDbContext context, ResourceService resourceService)
    {
        _context = context;
        _resourceService = resourceService;
    }

    // POST: api/UserState/mood
    // Пользователь ставит оценку настроения (1-5)
    [HttpPost("mood")]
    public async Task<IActionResult> SetMood([FromBody] MoodRequest request)
    {
        if (request.MoodLevel < 1 || request.MoodLevel > 5)
            return BadRequest("Настроение должно быть от 1 до 5");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Проверяем, была ли уже запись сегодня
        var state = await _context.UserStates
            .FirstOrDefaultAsync(us => us.UserId == request.UserId && us.Date == today);

        if (state == null)
        {
            state = new UserState
            {
                UserId = request.UserId,
                Date = today,
                MoodLevel = request.MoodLevel,
                ResourceLevel = 100
            };
            _context.UserStates.Add(state);
        }
        else
        {
            state.MoodLevel = request.MoodLevel;
        }

        await _context.SaveChangesAsync();
        
        var currentResource = await _resourceService.CalculateResourceAsync(request.UserId);
        return Ok(new { Message = "Настроение сохранено", CurrentResource = currentResource });
    }

    // GET: api/UserState/{userId}/resource
    // Узнать текущий уровень ресурса
    [HttpGet("{userId}/resource")]
    public async Task<ActionResult<ResourceResponse>> GetResource(Guid userId)
    {
        var level = await _resourceService.CalculateResourceAsync(userId);
        
        string msg = level switch
        {
            > 80 => "Вы полны энергии, отличное время для встреч",
            > 50 => "Нормальный уровень, можно идти на встречу",
            > 20 => "Вы устали, лучше выбрать короткие встречи",
            _ => "Вы очень сильно устали, рекомендуется отдых"
        };

        return Ok(new ResourceResponse { ResourceLevel = level, StatusMessage = msg });
    }
}