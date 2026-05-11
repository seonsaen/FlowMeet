using FlowMeet.Server.Models.DTOs;

namespace FlowMeet.Server.Services;

public interface IGroupService
{
    Task<(bool IsSuccess, string ErrorMessage, GroupResponse? Group)> CreateGroupAsync(Guid currentUserId, CreateGroupRequest request);
    Task<(bool IsSuccess, string ErrorMessage, List<GroupResponse> Groups)> GetUserGroupsAsync(Guid currentUserId);
    Task<(bool IsSuccess, string ErrorMessage, GroupResponse? Group)> UpdateGroupAsync(Guid currentUserId, Guid groupId, UpdateGroupRequest request);
    Task<(bool IsSuccess, string ErrorMessage)> DeleteGroupAsync(Guid currentUserId, Guid groupId);
    Task<(bool IsSuccess, string ErrorMessage, GroupInviteResponse? Invite)> InviteToGroupAsync(Guid currentUserId, InviteToGroupRequest request);
    Task<List<GroupIncomingInviteDto>> GetIncomingInvitesAsync(Guid currentUserId);
    Task<(bool IsSuccess, string ErrorMessage)> RespondToInviteAsync(Guid currentUserId, RespondToGroupInviteRequest request);
    Task<(bool IsSuccess, string ErrorMessage, GroupResponse? Group)> UpdateMemberRoleAsync(Guid currentUserId, Guid groupId, Guid memberId, UpdateGroupMemberRoleRequest request);
    Task<(bool IsSuccess, string ErrorMessage, GroupResponse? Group)> RemoveMemberAsync(Guid currentUserId, Guid groupId, Guid memberId);
    Task<(bool IsSuccess, string ErrorMessage)> LeaveGroupAsync(Guid currentUserId, Guid groupId);
}
