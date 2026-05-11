using FlowMeet.Server.Models.DTOs;
using FlowMeet.Server.Models.Entities;
using FlowMeet.Server.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace FlowMeet.Server.Tests;

public class GroupServiceTests
{
    [Fact]
    public async Task UpdateMemberRoleAsync_OwnerCanPromoteMemberToAdmin()
    {
        await using var context = TestDbFactory.CreateContext();
        var notifications = new RecordingNotificationService();
        var (groupId, ownerId, _, memberId) = await SeedGroupAsync(context);
        var service = new GroupService(context, notifications, new StubPlanningService());

        var result = await service.UpdateMemberRoleAsync(ownerId, groupId, memberId, new UpdateGroupMemberRoleRequest
        {
            Role = GroupRole.Admin
        });

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Group);
        Assert.Contains(result.Group!.Members, member => member.UserId == memberId && member.Role == GroupRole.Admin);
        Assert.Single(notifications.CreatedNotifications);
    }

    [Fact]
    public async Task UpdateMemberRoleAsync_AdminCannotChangeRoles()
    {
        await using var context = TestDbFactory.CreateContext();
        var notifications = new RecordingNotificationService();
        var (groupId, _, adminId, memberId) = await SeedGroupAsync(context);
        var service = new GroupService(context, notifications, new StubPlanningService());

        var result = await service.UpdateMemberRoleAsync(adminId, groupId, memberId, new UpdateGroupMemberRoleRequest
        {
            Role = GroupRole.Admin
        });

        Assert.False(result.IsSuccess);
        Assert.Equal("У вас нет прав менять роли участников", result.ErrorMessage);
    }

    [Fact]
    public async Task RemoveMemberAsync_AdminCanRemoveRegularMember()
    {
        await using var context = TestDbFactory.CreateContext();
        var notifications = new RecordingNotificationService();
        var (groupId, _, adminId, memberId) = await SeedGroupAsync(context);
        var service = new GroupService(context, notifications, new StubPlanningService());

        var result = await service.RemoveMemberAsync(adminId, groupId, memberId);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Group);
        Assert.DoesNotContain(result.Group!.Members, member => member.UserId == memberId);
        Assert.Single(notifications.CreatedNotifications);
    }

    [Fact]
    public async Task LeaveGroupAsync_BlocksOwnerButAllowsMember()
    {
        await using var context = TestDbFactory.CreateContext();
        var notifications = new RecordingNotificationService();
        var (groupId, ownerId, _, memberId) = await SeedGroupAsync(context);
        var service = new GroupService(context, notifications, new StubPlanningService());

        var ownerResult = await service.LeaveGroupAsync(ownerId, groupId);
        var memberResult = await service.LeaveGroupAsync(memberId, groupId);

        Assert.False(ownerResult.IsSuccess);
        Assert.Equal("Нельзя покинуть группу владельцу", ownerResult.ErrorMessage);
        Assert.True(memberResult.IsSuccess);
        Assert.DoesNotContain(context.GroupMembers, member => member.GroupId == groupId && member.UserId == memberId);
    }

    [Fact]
    public async Task InviteToGroupAsync_AllowsReinviteAfterMemberWasRemoved()
    {
        await using var context = TestDbFactory.CreateContext();
        var notifications = new RecordingNotificationService();
        var ownerId = Guid.NewGuid();
        var inviteeId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        context.Users.AddRange(
            new User
            {
                Id = ownerId,
                Email = "owner@example.com", 
                PasswordHash = "hash",
                FirstName = "Owner", 
                LastName = "User"
            },
            new User
            {
                Id = inviteeId, 
                Email = "invitee@example.com", 
                PasswordHash = "hash", 
                FirstName = "Invitee", 
                LastName = "User"
            });

        context.Groups.Add(new Group
        {
            Id = groupId,
            OwnerId = ownerId,
            Name = "Тестовая группа",
            CreatedDate = DateTime.UtcNow
        });

        context.GroupMembers.Add(new GroupMember
        {
            GroupId = groupId,
            UserId = ownerId,
            Role = GroupRole.Owner,
            JoinDate = DateTime.UtcNow
        });

        context.Friendships.Add(new Friendship
        {
            RequesterId = ownerId,
            AddresseeId = inviteeId,
            Status = FriendshipStatus.Accepted
        });

        context.GroupInvites.Add(new GroupInvite
        {
            GroupId = groupId,
            InviteeId = inviteeId,
            InviterId = ownerId,
            Status = GroupInviteStatus.Accepted,
            CreatedDate = DateTime.UtcNow.AddDays(-2)
        });

        await context.SaveChangesAsync();

        var service = new GroupService(context, notifications, new StubPlanningService());

        var result = await service.InviteToGroupAsync(ownerId, new InviteToGroupRequest
        {
            GroupId = groupId,
            InviteeId = inviteeId
        });

        var storedInvite = context.GroupInvites.Single();

        Assert.True(result.IsSuccess);
        Assert.Equal(GroupInviteStatus.Pending, storedInvite.Status);
        Assert.Single(notifications.CreatedNotifications);
    }

    [Fact]
    public async Task InviteToGroupAsync_BlocksDirectInviteForNonFriend()
    {
        await using var context = TestDbFactory.CreateContext();
        var notifications = new RecordingNotificationService();
        var ownerId = Guid.NewGuid();
        var inviteeId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        context.Users.AddRange(
            new User
            {
                Id = ownerId, 
                Email = "owner@example.com", 
                PasswordHash = "hash", 
                FirstName = "Owner", 
                LastName = "User"
            },
            new User { 
                Id = inviteeId, 
                Email = "invitee@example.com", 
                PasswordHash = "hash", 
                FirstName = "Invitee", 
                LastName = "User"
                
            });

        context.Groups.Add(new Group
        {
            Id = groupId,
            OwnerId = ownerId,
            Name = "Тестовая группа",
            CreatedDate = DateTime.UtcNow
        });

        context.GroupMembers.Add(new GroupMember
        {
            GroupId = groupId,
            UserId = ownerId,
            Role = GroupRole.Owner,
            JoinDate = DateTime.UtcNow
        });

        await context.SaveChangesAsync();

        var service = new GroupService(context, notifications, new StubPlanningService());

        var result = await service.InviteToGroupAsync(ownerId, new InviteToGroupRequest
        {
            GroupId = groupId,
            InviteeId = inviteeId
        });

        Assert.False(result.IsSuccess);
        Assert.Equal("Пригласить в группу напрямую можно только пользователя из списка друзей", result.ErrorMessage);
        Assert.Empty(context.GroupInvites);
        Assert.Empty(notifications.CreatedNotifications);
    }

    [Fact]
    public async Task InviteToGroupAsync_BlocksRegularMemberFromInviting()
    {
        await using var context = TestDbFactory.CreateContext();
        var notifications = new RecordingNotificationService();
        var (groupId, _, _, memberId) = await SeedGroupAsync(context);
        var inviteeId = Guid.NewGuid();

        context.Users.Add(new User
        {
            Id = inviteeId,
            Email = "invitee@example.com",
            PasswordHash = "hash",
            FirstName = "Invitee",
            LastName = "User"
        });

        context.Friendships.Add(new Friendship
        {
            RequesterId = memberId,
            AddresseeId = inviteeId,
            Status = FriendshipStatus.Accepted
        });

        await context.SaveChangesAsync();

        var service = new GroupService(context, notifications, new StubPlanningService());

        var result = await service.InviteToGroupAsync(memberId, new InviteToGroupRequest
        {
            GroupId = groupId,
            InviteeId = inviteeId
        });

        Assert.False(result.IsSuccess);
        Assert.Equal("У вас нет прав приглашать пользователей в группу", result.ErrorMessage);
        Assert.Empty(context.GroupInvites);
        Assert.Empty(notifications.CreatedNotifications);
    }

    private static async Task<(Guid GroupId, Guid OwnerId, Guid AdminId, Guid MemberId)> SeedGroupAsync(FlowMeet.Server.Data.AppDbContext context)
    {
        var ownerId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        context.Users.AddRange(
            new User
            {
                Id = ownerId,
                Email = "owner@example.com", 
                PasswordHash = "hash",
                FirstName = "Owner", 
                LastName = "User"
            },
            new User
            {
                Id = adminId, 
                Email = "admin@example.com", 
                PasswordHash = "hash",
                FirstName = "Admin", 
                LastName = "User"
            },
            new User { 
                Id = memberId,
                Email = "member@example.com", 
                PasswordHash = "hash", 
                FirstName = "Member", 
                LastName = "User" 
            });

        context.Groups.Add(new Group
        {
            Id = groupId,
            OwnerId = ownerId,
            Name = "Тестовая группа",
            Description = "Описание",
            CreatedDate = DateTime.UtcNow
        });

        context.GroupMembers.AddRange(
            new GroupMember { GroupId = groupId, UserId = ownerId, Role = GroupRole.Owner, JoinDate = DateTime.UtcNow },
            new GroupMember { GroupId = groupId, UserId = adminId, Role = GroupRole.Admin, JoinDate = DateTime.UtcNow },
            new GroupMember { GroupId = groupId, UserId = memberId, Role = GroupRole.Member, JoinDate = DateTime.UtcNow });

        await context.SaveChangesAsync();
        return (groupId, ownerId, adminId, memberId);
    }
}
