using FlowMeet.Server.Controllers;
using FlowMeet.Server.Models.DTOs;
using FlowMeet.Server.Models.Entities;
using FlowMeet.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace FlowMeet.Server.Tests;

public class GroupControllerTests
{
    [Fact]
    public async Task CreateGroup_ReturnsOkGroup()
    {
        var userId = Guid.NewGuid();
        var expectedGroup = new GroupResponse
        {
            Id = Guid.NewGuid(),
            Name = "Проект"
        };
        var service = new FakeGroupService
        {
            CreateResult = (true, string.Empty, expectedGroup)
        };
        var controller = new GroupController(service);
        ControllerTestHelper.SetUser(controller, userId);

        var request = new CreateGroupRequest
        {
            Name = "Проект"
        };

        var result = await controller.CreateGroup(request);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var group = Assert.IsType<GroupResponse>(ok.Value);
        Assert.Equal(userId, service.LastCreateUserId);
        Assert.Same(request, service.LastCreateRequest);
        Assert.Equal(expectedGroup.Id, group.Id);
    }

    [Fact]
    public async Task GetMyGroups_ServiceErrorMapsToNotFound()
    {
        var service = new FakeGroupService
        {
            GetGroupsResult = (false, "Пользователь не найден", new List<GroupResponse>())
        };
        var controller = new GroupController(service);
        ControllerTestHelper.SetUser(controller, Guid.NewGuid());

        var result = await controller.GetMyGroups();

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task UpdateGroup_WithoutUser_ReturnsUnauthorized()
    {
        var service = new FakeGroupService();
        var controller = new GroupController(service);
        ControllerTestHelper.SetUser(controller);

        var result = await controller.UpdateGroup(Guid.NewGuid(), new UpdateGroupRequest
        {
            Name = "Новое имя"
        });

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    [Fact]
    public async Task DeleteGroup_SuccessReturnsMessage()
    {
        var userId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var service = new FakeGroupService
        {
            DeleteResult = (true, string.Empty)
        };
        var controller = new GroupController(service);
        ControllerTestHelper.SetUser(controller, userId);

        var result = await controller.DeleteGroup(groupId);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(userId, service.LastDeleteUserId);
        Assert.Equal(groupId, service.LastDeleteGroupId);
        Assert.Equal("Группа удалена", ControllerTestHelper.GetValue<string>(ok.Value!, "message"));
    }

    [Fact]
    public async Task InviteToGroup_ReturnsOkInvite()
    {
        var userId = Guid.NewGuid();
        var invite = new GroupInviteResponse
        {
            GroupId = Guid.NewGuid(),
            GroupName = "Проект",
            InviterId = userId,
            InviterName = "Ваня",
            Status = GroupInviteStatus.Pending
        };
        var service = new FakeGroupService
        {
            InviteResult = (true, string.Empty, invite)
        };
        var controller = new GroupController(service);
        ControllerTestHelper.SetUser(controller, userId);

        var request = new InviteToGroupRequest
        {
            GroupId = invite.GroupId,
            InviteeId = Guid.NewGuid()
        };

        var result = await controller.InviteToGroup(request);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<GroupInviteResponse>(ok.Value);
        Assert.Same(request, service.LastInviteRequest);
        Assert.Equal(invite.GroupName, response.GroupName);
    }

    [Fact]
    public async Task GetIncomingInvites_ReturnsOkList()
    {
        var userId = Guid.NewGuid();
        var service = new FakeGroupService
        {
            IncomingInvites = new List<GroupIncomingInviteDto>
            {
                new()
                {
                    GroupId = Guid.NewGuid(),
                    GroupName = "Проект",
                    InviterId = Guid.NewGuid(),
                    InviterName = "Ваня"
                }
            }
        };
        var controller = new GroupController(service);
        ControllerTestHelper.SetUser(controller, userId);

        var result = await controller.GetIncomingInvites();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var invites = Assert.IsType<List<GroupIncomingInviteDto>>(ok.Value);
        Assert.Equal(userId, service.LastIncomingUserId);
        Assert.Single(invites);
    }

    [Fact]
    public async Task RespondToInvite_DeclinedReturnsMessage()
    {
        var userId = Guid.NewGuid();
        var request = new RespondToGroupInviteRequest
        {
            GroupId = Guid.NewGuid(),
            IsAccepted = false
        };
        var service = new FakeGroupService
        {
            RespondResult = (true, string.Empty)
        };
        var controller = new GroupController(service);
        ControllerTestHelper.SetUser(controller, userId);

        var result = await controller.RespondToInvite(request);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(userId, service.LastRespondUserId);
        Assert.Same(request, service.LastRespondRequest);
        Assert.Equal("Приглашение в группу отклонено", ControllerTestHelper.GetValue<string>(ok.Value!, "message"));
    }

    [Fact]
    public async Task UpdateMemberRole_ServiceErrorMapsToForbidden()
    {
        var service = new FakeGroupService
        {
            UpdateRoleResult = (false, "У вас нет прав изменить роль участника", null)
        };
        var controller = new GroupController(service);
        ControllerTestHelper.SetUser(controller, Guid.NewGuid());

        var result = await controller.UpdateMemberRole(Guid.NewGuid(), Guid.NewGuid(), new UpdateGroupMemberRoleRequest
        {
            Role = GroupRole.Admin
        });

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(403, objectResult.StatusCode);
    }

    [Fact]
    public async Task RemoveMember_ReturnsUpdatedGroup()
    {
        var service = new FakeGroupService
        {
            RemoveMemberResult = (true, string.Empty, new GroupResponse
            {
                Id = Guid.NewGuid(),
                Name = "Новая группа"
            })
        };
        var controller = new GroupController(service);
        ControllerTestHelper.SetUser(controller, Guid.NewGuid());

        var result = await controller.RemoveMember(Guid.NewGuid(), Guid.NewGuid());

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var group = Assert.IsType<GroupResponse>(ok.Value);
        Assert.Equal("Новая группа", group.Name);
    }

    [Fact]
    public async Task LeaveGroup_SuccessReturnsMessage()
    {
        var userId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var service = new FakeGroupService
        {
            LeaveResult = (true, string.Empty)
        };
        var controller = new GroupController(service);
        ControllerTestHelper.SetUser(controller, userId);

        var result = await controller.LeaveGroup(groupId);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(userId, service.LastLeaveUserId);
        Assert.Equal(groupId, service.LastLeaveGroupId);
        Assert.Equal("Вы покинули группу", ControllerTestHelper.GetValue<string>(ok.Value!, "message"));
    }

    private sealed class FakeGroupService : IGroupService
    {
        public (bool IsSuccess, string ErrorMessage, GroupResponse? Group) CreateResult { get; set; } = (true, string.Empty, new GroupResponse());
        public (bool IsSuccess, string ErrorMessage, List<GroupResponse> Groups) GetGroupsResult { get; set; } = (true, string.Empty, new List<GroupResponse>());
        public (bool IsSuccess, string ErrorMessage, GroupResponse? Group) UpdateGroupResult { get; set; } = (true, string.Empty, new GroupResponse());
        public (bool IsSuccess, string ErrorMessage) DeleteResult { get; set; } = (true, string.Empty);
        public (bool IsSuccess, string ErrorMessage, GroupInviteResponse? Invite) InviteResult { get; set; } = (true, string.Empty, new GroupInviteResponse());
        public List<GroupIncomingInviteDto> IncomingInvites { get; set; } = new();
        public (bool IsSuccess, string ErrorMessage) RespondResult { get; set; } = (true, string.Empty);
        public (bool IsSuccess, string ErrorMessage, GroupResponse? Group) UpdateRoleResult { get; set; } = (true, string.Empty, new GroupResponse());
        public (bool IsSuccess, string ErrorMessage, GroupResponse? Group) RemoveMemberResult { get; set; } = (true, string.Empty, new GroupResponse());
        public (bool IsSuccess, string ErrorMessage) LeaveResult { get; set; } = (true, string.Empty);
        public Guid LastCreateUserId { get; private set; }
        public CreateGroupRequest? LastCreateRequest { get; private set; }
        public Guid LastDeleteUserId { get; private set; }
        public Guid LastDeleteGroupId { get; private set; }
        public InviteToGroupRequest? LastInviteRequest { get; private set; }
        public Guid LastIncomingUserId { get; private set; }
        public Guid LastRespondUserId { get; private set; }
        public RespondToGroupInviteRequest? LastRespondRequest { get; private set; }
        public Guid LastLeaveUserId { get; private set; }
        public Guid LastLeaveGroupId { get; private set; }

        public Task<(bool IsSuccess, string ErrorMessage, GroupResponse? Group)> CreateGroupAsync(Guid currentUserId, CreateGroupRequest request)
        {
            LastCreateUserId = currentUserId;
            LastCreateRequest = request;
            return Task.FromResult(CreateResult);
        }

        public Task<(bool IsSuccess, string ErrorMessage, List<GroupResponse> Groups)> GetUserGroupsAsync(Guid currentUserId)
            => Task.FromResult(GetGroupsResult);

        public Task<(bool IsSuccess, string ErrorMessage, GroupResponse? Group)> UpdateGroupAsync(Guid currentUserId, Guid groupId, UpdateGroupRequest request)
            => Task.FromResult(UpdateGroupResult);

        public Task<(bool IsSuccess, string ErrorMessage)> DeleteGroupAsync(Guid currentUserId, Guid groupId)
        {
            LastDeleteUserId = currentUserId;
            LastDeleteGroupId = groupId;
            return Task.FromResult(DeleteResult);
        }

        public Task<(bool IsSuccess, string ErrorMessage, GroupInviteResponse? Invite)> InviteToGroupAsync(Guid currentUserId, InviteToGroupRequest request)
        {
            LastInviteRequest = request;
            return Task.FromResult(InviteResult);
        }

        public Task<List<GroupIncomingInviteDto>> GetIncomingInvitesAsync(Guid currentUserId)
        {
            LastIncomingUserId = currentUserId;
            return Task.FromResult(IncomingInvites);
        }

        public Task<(bool IsSuccess, string ErrorMessage)> RespondToInviteAsync(Guid currentUserId, RespondToGroupInviteRequest request)
        {
            LastRespondUserId = currentUserId;
            LastRespondRequest = request;
            return Task.FromResult(RespondResult);
        }

        public Task<(bool IsSuccess, string ErrorMessage, GroupResponse? Group)> UpdateMemberRoleAsync(Guid currentUserId, Guid groupId, Guid memberId, UpdateGroupMemberRoleRequest request)
            => Task.FromResult(UpdateRoleResult);

        public Task<(bool IsSuccess, string ErrorMessage, GroupResponse? Group)> RemoveMemberAsync(Guid currentUserId, Guid groupId, Guid memberId)
            => Task.FromResult(RemoveMemberResult);

        public Task<(bool IsSuccess, string ErrorMessage)> LeaveGroupAsync(Guid currentUserId, Guid groupId)
        {
            LastLeaveUserId = currentUserId;
            LastLeaveGroupId = groupId;
            return Task.FromResult(LeaveResult);
        }
    }
}
