using FlowMeet.Server.Controllers;
using FlowMeet.Server.Models.DTOs;
using FlowMeet.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace FlowMeet.Server.Tests;

public class SocialControllerTests
{
    [Fact]
    public async Task SendFriendRequest_WithoutUser_ReturnsUnauthorized()
    {
        var service = new FakeSocialService();
        var controller = new SocialController(service);
        ControllerTestHelper.SetUser(controller);

        var result = await controller.SendFriendRequest(new FriendRequest
        {
            TargetEmail = "friend@example.com"
        });

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task SendFriendRequest_SuccessReturnsMessage()
    {
        var userId = Guid.NewGuid();
        var request = new FriendRequest
        {
            TargetEmail = "friend@example.com"
        };
        var service = new FakeSocialService
        {
            SendResult = (true, string.Empty)
        };
        var controller = new SocialController(service);
        ControllerTestHelper.SetUser(controller, userId);

        var result = await controller.SendFriendRequest(request);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(userId, service.LastSendUserId);
        Assert.Same(request, service.LastSendRequest);
        Assert.Equal("Заявка в друзья отправлена", ControllerTestHelper.GetValue<string>(ok.Value!, "message"));
    }

    [Fact]
    public async Task GetIncomingRequests_ReturnsOkList()
    {
        var userId = Guid.NewGuid();
        var service = new FakeSocialService
        {
            IncomingRequests = new List<AcceptFriendRequest>
            {
                new()
                {
                    RequesterId = Guid.NewGuid(),
                    RequesterName = "Ваня"
                }
            }
        };
        var controller = new SocialController(service);
        ControllerTestHelper.SetUser(controller, userId);

        var result = await controller.GetIncomingRequests();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var requests = Assert.IsType<List<AcceptFriendRequest>>(ok.Value);
        Assert.Equal(userId, service.LastIncomingUserId);
        Assert.Single(requests);
    }

    [Fact]
    public async Task AcceptRequest_ServiceErrorMapsToNotFound()
    {
        var service = new FakeSocialService
        {
            AcceptResult = (false, "Заявка не найдена")
        };
        var controller = new SocialController(service);
        ControllerTestHelper.SetUser(controller, Guid.NewGuid());

        var result = await controller.AcceptRequest(new AcceptFriendRequest
        {
            RequesterId = Guid.NewGuid()
        });

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task DeclineRequest_SuccessReturnsMessage()
    {
        var userId = Guid.NewGuid();
        var request = new AcceptFriendRequest
        {
            RequesterId = Guid.NewGuid()
        };
        var service = new FakeSocialService
        {
            DeclineResult = (true, string.Empty)
        };
        var controller = new SocialController(service);
        ControllerTestHelper.SetUser(controller, userId);

        var result = await controller.DeclineRequest(request);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(userId, service.LastDeclineUserId);
        Assert.Same(request, service.LastDeclineRequest);
        Assert.Equal("Заявка отклонена", ControllerTestHelper.GetValue<string>(ok.Value!, "message"));
    }

    [Fact]
    public async Task GetFriends_ReturnsOkList()
    {
        var userId = Guid.NewGuid();
        var service = new FakeSocialService
        {
            Friends = new List<FriendDto>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    FullName = "Ваня 67"
                }
            }
        };
        var controller = new SocialController(service);
        ControllerTestHelper.SetUser(controller, userId);

        var result = await controller.GetFriends();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var friends = Assert.IsType<List<FriendDto>>(ok.Value);
        Assert.Equal(userId, service.LastFriendsUserId);
        Assert.Single(friends);
    }

    [Fact]
    public async Task DeleteFriend_SuccessReturnsTruePayload()
    {
        var userId = Guid.NewGuid();
        var friendId = Guid.NewGuid();
        var service = new FakeSocialService
        {
            DeleteResult = (true, string.Empty)
        };
        var controller = new SocialController(service);
        ControllerTestHelper.SetUser(controller, userId);

        var result = await controller.DeleteFriend(friendId);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(userId, service.LastDeleteUserId);
        Assert.Equal(friendId, service.LastDeleteFriendId);
        Assert.True(ControllerTestHelper.GetValue<bool>(ok.Value!, "isSuccess"));
    }

    private sealed class FakeSocialService : ISocialService
    {
        public (bool IsSuccess, string ErrorMessage) SendResult { get; set; } = (true, string.Empty);
        public List<AcceptFriendRequest> IncomingRequests { get; set; } = new();
        public (bool IsSuccess, string ErrorMessage) AcceptResult { get; set; } = (true, string.Empty);
        public (bool IsSuccess, string ErrorMessage) DeclineResult { get; set; } = (true, string.Empty);
        public List<FriendDto> Friends { get; set; } = new();
        public (bool IsSuccess, string ErrorMessage) DeleteResult { get; set; } = (true, string.Empty);
        public Guid LastSendUserId { get; private set; }
        public FriendRequest? LastSendRequest { get; private set; }
        public Guid LastIncomingUserId { get; private set; }
        public Guid LastDeclineUserId { get; private set; }
        public AcceptFriendRequest? LastDeclineRequest { get; private set; }
        public Guid LastFriendsUserId { get; private set; }
        public Guid LastDeleteUserId { get; private set; }
        public Guid LastDeleteFriendId { get; private set; }

        public Task<(bool IsSuccess, string ErrorMessage)> SendFriendRequestAsync(Guid userId, FriendRequest request)
        {
            LastSendUserId = userId;
            LastSendRequest = request;
            return Task.FromResult(SendResult);
        }

        public Task<List<AcceptFriendRequest>> GetIncomingFriendRequestsAsync(Guid userId)
        {
            LastIncomingUserId = userId;
            return Task.FromResult(IncomingRequests);
        }

        public Task<(bool IsSuccess, string ErrorMessage)> AcceptRequestAsync(Guid userId, AcceptFriendRequest request)
            => Task.FromResult(AcceptResult);

        public Task<(bool IsSuccess, string ErrorMessage)> DeclineRequestAsync(Guid userId, AcceptFriendRequest request)
        {
            LastDeclineUserId = userId;
            LastDeclineRequest = request;
            return Task.FromResult(DeclineResult);
        }

        public Task<List<FriendDto>> GetFriendsAsync(Guid userId)
        {
            LastFriendsUserId = userId;
            return Task.FromResult(Friends);
        }

        public Task<(bool IsSuccess, string ErrorMessage)> DeleteFriendAsync(Guid userId, Guid friendId)
        {
            LastDeleteUserId = userId;
            LastDeleteFriendId = friendId;
            return Task.FromResult(DeleteResult);
        }
    }
}
