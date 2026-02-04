using FlowMeet.Server.Data;
using FlowMeet.Server.Models.Entities;
using FlowMeet.Server.Models.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FlowMeet.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SocialController : ControllerBase
{
    private readonly AppDbContext _context;

    public SocialController(AppDbContext context)
    {
        _context = context;
    }

    // POST: api/Social/request
    // Отправить заявку в друзья по Email
    [HttpPost("request")]
    public async Task<IActionResult> SendFriendRequest([FromBody] FriendRequest request)
    {
        // Ищем друга по email
        var targetUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.TargetEmail);
        if (targetUser == null)
            return NotFound("Пользователь с таким email не найден");

        if (targetUser.Id == request.RequesterId)
            return BadRequest("Нельзя добавить в друзья самого себя");

        // Проверяем, нет ли уже связи
        var existing = await _context.Friendships
            .FirstOrDefaultAsync(f => 
                (f.RequesterId == request.RequesterId && f.AddresseeId == targetUser.Id) ||
                (f.RequesterId == targetUser.Id && f.AddresseeId == request.RequesterId));

        if (existing != null)
            return BadRequest("Заявка уже отправлена или вы уже друзья");

        // Создаем заявку
        var friendship = new Friendship
        {
            RequesterId = request.RequesterId,
            AddresseeId = targetUser.Id,
            Status = FriendshipStatus.Pending // Статус "Ожидает"
        };

        _context.Friendships.Add(friendship);
        await _context.SaveChangesAsync();

        return Ok("Заявка в друзья отправлена");
    }

    // POST: api/Social/accept
    // Принять заявку
    [HttpPost("accept")]
    public async Task<IActionResult> AcceptRequest(Guid requesterId, Guid myId)
    {
        var friendship = await _context.Friendships
            .FirstOrDefaultAsync(f => f.RequesterId == requesterId && f.AddresseeId == myId);

        if (friendship == null)
            return NotFound("Заявка не найдена");

        friendship.Status = FriendshipStatus.Accepted;
        await _context.SaveChangesAsync();

        return Ok("Теперь вы друзья!");
    }

    // GET: api/Social/{userId}/friends
    // Получить список моих друзей
    [HttpGet("{userId}/friends")]
    public async Task<ActionResult<List<FriendDto>>> GetFriends(Guid userId)
    {
        var friendships = await _context.Friendships
            .Include(f => f.Requester)
            .Include(f => f.Addressee)
            .Where(f => (f.RequesterId == userId || f.AddresseeId == userId) 
                        && f.Status == FriendshipStatus.Accepted)
            .ToListAsync();

        var result = new List<FriendDto>();

        foreach (var f in friendships)
        {
            var friendUser = f.RequesterId == userId ? f.Addressee : f.Requester;

            result.Add(new FriendDto
            {
                Id = friendUser.Id,
                FullName = $"{friendUser.FirstName} {friendUser.LastName}",
                Email = friendUser.Email,
                Status = "Accepted"
            });
        }

        return Ok(result);
    }
}