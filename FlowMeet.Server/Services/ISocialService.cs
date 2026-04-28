using FlowMeet.Server.Models.DTOs;

namespace FlowMeet.Server.Services;

public interface ISocialService
{
    Task<(bool IsSuccess, string ErrorMessage)> SendFriendRequestAsync(Guid userId, FriendRequest request);
    Task<List<AcceptFriendRequest>> GetIncomingFriendRequestsAsync(Guid userId);
    Task<(bool IsSuccess, string ErrorMessage)> AcceptRequestAsync(Guid userId, AcceptFriendRequest request);
    Task<(bool IsSuccess, string ErrorMessage)> DeclineRequestAsync(Guid userId, AcceptFriendRequest request);
    Task<List<FriendDto>> GetFriendsAsync(Guid userId);
    Task<(bool IsSuccess, string ErrorMessage)> DeleteFriendAsync(Guid userId, Guid friendId);
}