using FlowMeet.Server.Data;
using FlowMeet.Server.Models.DTOs;
using FlowMeet.Server.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace FlowMeet.Server.Services;

public class GroupService : IGroupService
{
    private readonly AppDbContext _context;
    private readonly INotificationService _notificationService;

    public GroupService(AppDbContext context, INotificationService notificationService)
    {
        _context = context;
        _notificationService = notificationService;
    }

    public async Task<(bool IsSuccess, string ErrorMessage, GroupResponse? Group)> CreateGroupAsync(Guid currentUserId, CreateGroupRequest request)
    {
        var owner = await _context.Users.FindAsync(currentUserId);
        if (owner == null)
            return (false, "Пользователь не найден", null);

        var group = new Group
        {
            Id = Guid.NewGuid(),
            OwnerId = currentUserId,
            Name = request.Name,
            Description = request.Description,
            CreatedDate = DateTime.UtcNow
        };

        var member = new GroupMember
        {
            GroupId = group.Id,
            UserId = currentUserId,
            Role = GroupRole.Owner,
            JoinDate = DateTime.UtcNow
        };

        _context.Groups.Add(group);
        _context.GroupMembers.Add(member);
        await _context.SaveChangesAsync();

        return (true, string.Empty, new GroupResponse
        {
            Id = group.Id,
            Name = group.Name,
            Description = group.Description,
            Members = new List<GroupMemberResponse>
            {
                new GroupMemberResponse
                {
                    UserId = owner.Id,
                    Name = owner.FirstName + " " + owner.LastName,
                    Email = owner.Email,
                    Role = GroupRole.Owner,
                    IsOwner = true,
                    JoinDate = member.JoinDate
                }
            }
        });
    }

    public async Task<(bool IsSuccess, string ErrorMessage, List<GroupResponse> Groups)> GetUserGroupsAsync(Guid currentUserId)
    {
        var userExists = await _context.Users.AnyAsync(u => u.Id == currentUserId);
        if (!userExists)
            return (false, "Пользователь не найден", new List<GroupResponse>());

        var groupMembers = await _context.GroupMembers
            .Include(gm => gm.Group)
            .ThenInclude(g => g!.Members)
            .ThenInclude(m => m.User)
            .Where(gm => gm.UserId == currentUserId)
            .ToListAsync();

        var result = groupMembers.Select(gm => new GroupResponse
        {
            Id = gm.Group!.Id,
            Name = gm.Group.Name,
            Description = gm.Group.Description,
            Members = gm.Group.Members.Select(m => new GroupMemberResponse
            {
                UserId = m.UserId,
                Name = m.User!.FirstName + " " + m.User.LastName,
                Email = m.User.Email,
                Role = m.Role,
                IsOwner = m.Role == GroupRole.Owner,
                JoinDate = m.JoinDate
            }).ToList()
        }).ToList();

        return (true, string.Empty, result);
    }

    public async Task<(bool IsSuccess, string ErrorMessage, GroupResponse? Group)> UpdateGroupAsync(Guid currentUserId, Guid groupId, UpdateGroupRequest request)
    {
        var group = await _context.Groups
            .Include(g => g.Members)
            .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(g => g.Id == groupId);

        if (group == null)
            return (false, "Группа не найдена", null);

        var membership = group.Members.FirstOrDefault(m => m.UserId == currentUserId);
        if (membership == null)
            return (false, "Вы не состоите в этой группе", null);

        if (membership.Role != GroupRole.Owner && membership.Role != GroupRole.Admin)
            return (false, "У вас нет прав редактировать группу", null);

        group.Name = request.Name;
        group.Description = request.Description;
        await _context.SaveChangesAsync();

        return (true, string.Empty, ToGroupResponse(group));
    }

    public async Task<(bool IsSuccess, string ErrorMessage)> DeleteGroupAsync(Guid currentUserId, Guid groupId)
    {
        var group = await _context.Groups
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == groupId);

        if (group == null)
            return (false, "Группа не найдена");

        var isOwner = group.OwnerId == currentUserId ||
                      group.Members.Any(m => m.UserId == currentUserId && m.Role == GroupRole.Owner);

        if (!isOwner)
            return (false, "Удалить группу может только владелец");

        _context.Groups.Remove(group);
        await _context.SaveChangesAsync();

        return (true, string.Empty);
    }

    public async Task<(bool IsSuccess, string ErrorMessage, GroupInviteResponse? Invite)> InviteToGroupAsync(Guid currentUserId, InviteToGroupRequest request)
    {
        var group = await _context.Groups.FindAsync(request.GroupId);
        if (group == null)
            return (false, "Группа не найдена", null);

        var inviter = await _context.Users.FindAsync(currentUserId);
        if (inviter == null)
            return (false, "Отправитель приглашения не найден", null);

        if (request.InviteeId == currentUserId)
            return (false, "Нельзя пригласить самого себя", null);

        var inviterMembership = await _context.GroupMembers
            .FirstOrDefaultAsync(gm => gm.GroupId == request.GroupId && gm.UserId == currentUserId);

        if (inviterMembership == null)
            return (false, "Вы не состоите в этой группе", null);

        if (inviterMembership.Role != GroupRole.Owner && inviterMembership.Role != GroupRole.Admin)
            return (false, "У вас нет прав приглашать пользователей в группу", null);

        var invitee = await _context.Users.FindAsync(request.InviteeId);
        if (invitee == null)
            return (false, "Пользователь для приглашения не найден", null);

        var isMember = await _context.GroupMembers
            .AnyAsync(gm => gm.GroupId == request.GroupId && gm.UserId == request.InviteeId);

        if (isMember)
            return (false, "Пользователь уже является членом группы", null);

        var existingInvite = await _context.GroupInvites
            .FirstOrDefaultAsync(i => i.GroupId == request.GroupId && i.InviteeId == request.InviteeId);

        GroupInvite invite;

        if (existingInvite != null)
        {
            if (existingInvite.Status == GroupInviteStatus.Pending)
                return (false, "Приглашение уже отправлено", null);

            if (existingInvite.Status == GroupInviteStatus.Accepted)
                return (false, "Пользователь уже принял приглашение", null);

            existingInvite.InviterId = currentUserId;
            existingInvite.Status = GroupInviteStatus.Pending;
            existingInvite.CreatedDate = DateTime.UtcNow;
            invite = existingInvite;
        }
        else
        {
            invite = new GroupInvite
            {
                GroupId = request.GroupId,
                InviterId = currentUserId,
                InviteeId = request.InviteeId,
                Status = GroupInviteStatus.Pending,
                CreatedDate = DateTime.UtcNow
            };

            _context.GroupInvites.Add(invite);
        }

        await _context.SaveChangesAsync();

        await _notificationService.CreateNotificationAsync(
            request.InviteeId,
            NotificationType.GroupInvite,
            "Новое приглашение в группу",
            $"Вас пригласили в группу «{group.Name}»",
            group.Id);

        return (true, string.Empty, new GroupInviteResponse
        {
            GroupId = group.Id,
            GroupName = group.Name,
            InviterId = currentUserId,
            InviterName = inviter.FirstName + " " + inviter.LastName,
            Status = invite.Status,
            CreatedDate = invite.CreatedDate
        });
    }

    public async Task<List<GroupIncomingInviteDto>> GetIncomingInvitesAsync(Guid currentUserId)
    {
        var invites = await _context.GroupInvites
            .Include(i => i.Group)
            .Include(i => i.Inviter)
            .Where(i => i.InviteeId == currentUserId && i.Status == GroupInviteStatus.Pending)
            .OrderByDescending(i => i.CreatedDate)
            .ToListAsync();

        return invites.Select(i => new GroupIncomingInviteDto
        {
            GroupId = i.GroupId,
            GroupName = i.Group!.Name,
            InviterId = i.InviterId,
            InviterName = i.Inviter!.FirstName + " " + i.Inviter.LastName,
            CreatedDate = i.CreatedDate
        }).ToList();
    }

    public async Task<(bool IsSuccess, string ErrorMessage)> RespondToInviteAsync(Guid currentUserId, RespondToGroupInviteRequest request)
    {
        var invite = await _context.GroupInvites
            .Include(i => i.Group)
            .FirstOrDefaultAsync(i => i.GroupId == request.GroupId && i.InviteeId == currentUserId);

        if (invite == null)
            return (false, "Приглашение не найдено");

        if (invite.Status != GroupInviteStatus.Pending)
            return (false, "Вы уже ответили на это приглашение");

        if (request.IsAccepted)
        {
            var alreadyMember = await _context.GroupMembers
                .AnyAsync(gm => gm.GroupId == request.GroupId && gm.UserId == currentUserId);

            if (!alreadyMember)
            {
                _context.GroupMembers.Add(new GroupMember
                {
                    GroupId = request.GroupId,
                    UserId = currentUserId,
                    Role = GroupRole.Member,
                    JoinDate = DateTime.UtcNow
                });
            }

            invite.Status = GroupInviteStatus.Accepted;
        }
        else
        {
            invite.Status = GroupInviteStatus.Declined;
        }

        await _context.SaveChangesAsync();
        return (true, string.Empty);
    }

    private static GroupResponse ToGroupResponse(Group group) => new()
    {
        Id = group.Id,
        Name = group.Name,
        Description = group.Description,
        Members = group.Members.Select(m => new GroupMemberResponse
        {
            UserId = m.UserId,
            Name = m.User == null ? string.Empty : m.User.FirstName + " " + m.User.LastName,
            Email = m.User?.Email ?? string.Empty,
            Role = m.Role,
            IsOwner = m.Role == GroupRole.Owner,
            JoinDate = m.JoinDate
        }).ToList()
    };
}
