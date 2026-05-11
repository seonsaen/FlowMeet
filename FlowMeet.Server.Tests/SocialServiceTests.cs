using FlowMeet.Server.Models.DTOs;
using FlowMeet.Server.Models.Entities;
using FlowMeet.Server.Services;

namespace FlowMeet.Server.Tests;

public class SocialServiceTests
{
    [Fact]
    public async Task DeleteFriendAsync_RemovesFriendshipRegardlessOfDirection()
    {
        await using var context = TestDbFactory.CreateContext();
        var notifications = new RecordingNotificationService();
        var firstUserId = Guid.NewGuid();
        var secondUserId = Guid.NewGuid();

        context.Users.AddRange(
            new User { Id = firstUserId, Email = "first@example.com", PasswordHash = "hash", FirstName = "First", LastName = "User" },
            new User { Id = secondUserId, Email = "second@example.com", PasswordHash = "hash", FirstName = "Second", LastName = "User" });
        context.Friendships.Add(new Friendship
        {
            RequesterId = firstUserId,
            AddresseeId = secondUserId,
            Status = FriendshipStatus.Accepted
        });
        await context.SaveChangesAsync();

        var service = new SocialService(context, notifications, new StubPlanningService());

        var result = await service.DeleteFriendAsync(secondUserId, firstUserId);

        Assert.True(result.IsSuccess);
        Assert.Empty(context.Friendships);
    }

    [Fact]
    public async Task SendFriendRequestAsync_ReusesDeclinedRequestFromSameDirection()
    {
        await using var context = TestDbFactory.CreateContext();
        var notifications = new RecordingNotificationService();
        var requesterId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        context.Users.AddRange(
            new User { Id = requesterId, Email = "requester@example.com", PasswordHash = "hash", FirstName = "Requester", LastName = "User" },
            new User { Id = targetId, Email = "target@example.com", PasswordHash = "hash", FirstName = "Target", LastName = "User" });
        context.Friendships.Add(new Friendship
        {
            RequesterId = requesterId,
            AddresseeId = targetId,
            Status = FriendshipStatus.Declined
        });
        await context.SaveChangesAsync();

        var service = new SocialService(context, notifications, new StubPlanningService());

        var result = await service.SendFriendRequestAsync(requesterId, new FriendRequest
        {
            TargetEmail = "target@example.com"
        });

        var friendship = context.Friendships.Single();

        Assert.True(result.IsSuccess);
        Assert.Equal(FriendshipStatus.Pending, friendship.Status);
        Assert.Single(notifications.CreatedNotifications);
    }

    [Fact]
    public async Task GetFriendsAsync_UsesLocalTimeForBaseScheduleBusyStatus()
    {
        await using var context = TestDbFactory.CreateContext();
        var notifications = new RecordingNotificationService();
        var currentUserId = Guid.NewGuid();
        var friendId = Guid.NewGuid();
        var localNow = DateTime.Now;
        var startTime = localNow.TimeOfDay - TimeSpan.FromMinutes(30);
        var endTime = localNow.TimeOfDay + TimeSpan.FromMinutes(30);

        if (startTime < TimeSpan.Zero)
            startTime = TimeSpan.Zero;

        if (endTime >= TimeSpan.FromDays(1))
            endTime = new TimeSpan(23, 59, 0);

        context.Users.AddRange(
            new User { Id = currentUserId, Email = "current@example.com", PasswordHash = "hash", FirstName = "Current", LastName = "User" },
            new User { Id = friendId, Email = "friend@example.com", PasswordHash = "hash", FirstName = "Friend", LastName = "User" });
        context.Friendships.Add(new Friendship
        {
            RequesterId = currentUserId,
            AddresseeId = friendId,
            Status = FriendshipStatus.Accepted
        });
        context.BaseScheduleEntries.Add(new BaseScheduleEntry
        {
            Id = Guid.NewGuid(),
            UserId = friendId,
            DayOfWeek = (int)DateOnly.FromDateTime(localNow).DayOfWeek,
            Title = "Local busy block",
            StartTime = startTime,
            EndTime = endTime,
            Type = EventType.Mandatory,
            EffectiveFromDate = DateOnly.FromDateTime(localNow).AddDays(-1)
        });
        await context.SaveChangesAsync();

        var service = new SocialService(context, notifications, new StubPlanningService());

        var result = await service.GetFriendsAsync(currentUserId);

        var friend = Assert.Single(result);
        Assert.True(friend.IsBusy);
        Assert.Equal("Занят", friend.Status);
    }
}
