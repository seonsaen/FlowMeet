using FlowMeet.Server.Data;
using FlowMeet.Server.Models.DTOs;
using FlowMeet.Server.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace FlowMeet.Server.Services;

public class SocialService : ISocialService
{
    private readonly AppDbContext _context;
    private readonly INotificationService _notificationService;

    public SocialService(AppDbContext context, INotificationService notificationService)
    {
        _context = context;
        _notificationService = notificationService;
    }

    public async Task<(bool IsSuccess, string ErrorMessage)> SendFriendRequestAsync(Guid userId, FriendRequest request)
    {
        var targetUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.TargetEmail);
        if (targetUser == null)
            return (false, "Пользователь с таким email не найден");

        if (targetUser.Id == userId)
            return (false, "Нельзя добавить в друзья самого себя");

        var existing = await _context.Friendships
            .FirstOrDefaultAsync(f => 
                (f.RequesterId == userId && f.AddresseeId == targetUser.Id) ||
                (f.RequesterId == targetUser.Id && f.AddresseeId == userId));

        if (existing != null)
        {
            if (existing.Status != FriendshipStatus.Declined)
                return (false, "Заявка уже отправлена или вы уже друзья");

            if (existing.RequesterId == userId && existing.AddresseeId == targetUser.Id)
            {
                existing.Status = FriendshipStatus.Pending;
                await _context.SaveChangesAsync();

                var retryRequester = await _context.Users.FindAsync(userId);
                await _notificationService.CreateNotificationAsync(
                    targetUser.Id,
                    NotificationType.FriendRequest,
                    "Новая заявка в друзья",
                    $"{retryRequester?.FirstName} {retryRequester?.LastName} хочет добавить вас в друзья",
                    userId);

                return (true, string.Empty);
            }

            _context.Friendships.Remove(existing);
            await _context.SaveChangesAsync();
        }

        var friendship = new Friendship
        {
            RequesterId = userId,
            AddresseeId = targetUser.Id,
            Status = FriendshipStatus.Pending 
        };

        _context.Friendships.Add(friendship);
        await _context.SaveChangesAsync();

        var requester = await _context.Users.FindAsync(userId);
        await _notificationService.CreateNotificationAsync(
            targetUser.Id,
            NotificationType.FriendRequest,
            "Новая заявка в друзья",
            $"{requester?.FirstName} {requester?.LastName} хочет добавить вас в друзья",
            userId);

        return (true, string.Empty);
    }

    public async Task<List<AcceptFriendRequest>> GetIncomingFriendRequestsAsync(Guid userId)
    {
        var friendRequests = await _context.Friendships.
            Where(f => f.AddresseeId == userId && f.Status == FriendshipStatus.Pending).ToListAsync();
        var result = new List<AcceptFriendRequest>();
        foreach (var f in friendRequests)
        {
            var requester = await _context.Users.FindAsync(f.RequesterId);
            if (requester != null)
            {
                result.Add(new AcceptFriendRequest
                    {
                        RequesterId = f.RequesterId,
                        RequesterName = requester.FirstName + " " + requester.LastName,
                        RequesterEmail = requester.Email,
                    }
                );
            }
        }
        return result;
    }

    public async Task<(bool IsSuccess, string ErrorMessage)> AcceptRequestAsync(Guid userId, AcceptFriendRequest request)
    {
        var friendship = await _context.Friendships
            .FirstOrDefaultAsync(f => f.RequesterId == request.RequesterId && f.AddresseeId == userId);

        if (friendship == null)
            return (false, "Заявка не найдена");

        if (friendship.Status == FriendshipStatus.Accepted)
            return (false, "Заявка уже принята");
        
        if (friendship.Status == FriendshipStatus.Declined)
            return (false, "Заявка уже отклонена");

        friendship.Status = FriendshipStatus.Accepted;
        await _context.SaveChangesAsync();

        return (true, string.Empty);
    }

    public async Task<(bool IsSuccess, string ErrorMessage)> DeclineRequestAsync(Guid userId,
        AcceptFriendRequest request)
    {
        var friendship = await _context.Friendships
            .FirstOrDefaultAsync(f => f.RequesterId == request.RequesterId && f.AddresseeId == userId);

        if (friendship == null)
            return (false, "Заявка не найдена");

        if (friendship.Status == FriendshipStatus.Accepted)
            return (false, "Заявка уже принята");
        
        if (friendship.Status == FriendshipStatus.Declined)
            return (false, "Заявка уже отклонена");
        
        friendship.Status = FriendshipStatus.Declined;
        await _context.SaveChangesAsync();
        
        return (true, string.Empty);
    }

    public async Task<List<FriendDto>> GetFriendsAsync(Guid userId)
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
            
            if (friendUser != null)
            {
                result.Add(new FriendDto
                {
                    Id = friendUser.Id,
                    FullName = $"{friendUser.FirstName} {friendUser.LastName}",
                    Email = friendUser.Email,
                    Status = "Accepted"
                });
            }
        }

        return result;
    }

    public async Task<(bool IsSuccess, string ErrorMessage)> DeleteFriendAsync(Guid userId, Guid friendId)
    {
        var friendship = await _context.Friendships.FirstOrDefaultAsync(f => f.RequesterId == userId && f.AddresseeId == friendId || 
                                                                             f.AddresseeId == userId  && f.RequesterId == friendId);
        if (friendship == null)
        {
            return (false, "");
        }
        
        _context.Friendships.Remove(friendship);
        await _context.SaveChangesAsync();
        return (true, string.Empty);
    }
}
