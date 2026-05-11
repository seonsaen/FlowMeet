using FlowMeet.Server.Data;
using FlowMeet.Server.Models.DTOs;
using FlowMeet.Server.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace FlowMeet.Server.Services;

public class GroupService : IGroupService
{
    private readonly AppDbContext _context;
    private readonly INotificationService _notificationService;
    private readonly IPlanningService _planningService;

    public GroupService(AppDbContext context, INotificationService notificationService, IPlanningService planningService)
    {
        _context = context;
        _notificationService = notificationService;
        _planningService = planningService;
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

        var result = groupMembers
            .Select(gm => ToGroupResponse(gm.Group!))
            .ToList();

        var groupIds = result.Select(group => group.Id).ToList();
        var upcomingMeetings = await _context.Meetings
            .AsNoTracking()
            .Include(meeting => meeting.RelatedGroup)
            .Include(meeting => meeting.Participants)
            .ThenInclude(participant => participant.User)
            .Where(meeting => meeting.Status == MeetingStatus.Confirmed
                              && meeting.RelatedGroupId != null
                              && groupIds.Contains(meeting.RelatedGroupId.Value)
                              && meeting.StartTime > DateTime.UtcNow)
            .OrderBy(meeting => meeting.StartTime)
            .ToListAsync();

        foreach (var group in result)
        {
            var upcomingMeeting = upcomingMeetings.FirstOrDefault(meeting => meeting.RelatedGroupId == group.Id);
            if (upcomingMeeting == null)
                continue;

            group.UpcomingMeeting = new ScheduledMeetingCardDto
            {
                MeetingId = upcomingMeeting.Id,
                Title = upcomingMeeting.Title,
                Description = upcomingMeeting.Description,
                StartTime = upcomingMeeting.StartTime,
                EndTime = upcomingMeeting.StartTime.Add(upcomingMeeting.Duration),
                RelatedGroupId = upcomingMeeting.RelatedGroupId,
                RelatedGroupName = upcomingMeeting.RelatedGroup?.Name
            };

            var participantIds = group.Members
                .Where(member => member.UserId != currentUserId)
                .Select(member => member.UserId)
                .Distinct()
                .ToList();

            if (participantIds.Count == 0)
                continue;

            var planningResult = await _planningService.FindGroupSlotsAsync(
                currentUserId,
                participantIds,
                DateOnly.FromDateTime(DateTime.UtcNow),
                Math.Max(15, (int)Math.Round(upcomingMeeting.Duration.TotalMinutes)));

            if (planningResult.IsSuccess)
                group.EarlierAvailableSlot = planningResult.Slots.FirstOrDefault(slot => slot.StartTime < upcomingMeeting.StartTime);
        }

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

        var areFriends = await _context.Friendships.AnyAsync(friendship =>
            friendship.Status == FriendshipStatus.Accepted
            && ((friendship.RequesterId == currentUserId && friendship.AddresseeId == request.InviteeId)
                || (friendship.RequesterId == request.InviteeId && friendship.AddresseeId == currentUserId)));

        if (!areFriends)
            return (false, "Пригласить в группу напрямую можно только пользователя из списка друзей", null);

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

    public async Task<(bool IsSuccess, string ErrorMessage, GroupResponse? Group)> UpdateMemberRoleAsync(Guid currentUserId, Guid groupId, Guid memberId, UpdateGroupMemberRoleRequest request)
    {
        var group = await LoadGroupAsync(groupId);
        if (group == null)
            return (false, "Группа не найдена", null);

        var actor = group.Members.FirstOrDefault(member => member.UserId == currentUserId);
        if (actor == null)
            return (false, "Вы не состоите в этой группе", null);

        if (actor.Role != GroupRole.Owner)
            return (false, "У вас нет прав менять роли участников", null);

        var target = group.Members.FirstOrDefault(member => member.UserId == memberId);
        if (target == null)
            return (false, "Участник группы не найден", null);

        if (target.UserId == currentUserId)
            return (false, "Нельзя менять собственную роль", null);

        if (target.Role == GroupRole.Owner)
            return (false, "Нельзя изменить роль владельца", null);

        if (request.Role == GroupRole.Owner)
            return (false, "Роль владельца нельзя назначить через этот endpoint", null);

        if (target.Role == request.Role)
            return (false, "Участник уже имеет эту роль", null);

        target.Role = request.Role;
        await _context.SaveChangesAsync();

        if (target.User != null)
        {
            await _notificationService.CreateNotificationAsync(
                target.UserId,
                NotificationType.Info,
                "Роль в группе изменена",
                $"В группе «{group.Name}» вам назначена роль {GetRoleLabel(request.Role)}",
                group.Id);
        }

        return (true, string.Empty, ToGroupResponse(group));
    }

    public async Task<(bool IsSuccess, string ErrorMessage, GroupResponse? Group)> RemoveMemberAsync(Guid currentUserId, Guid groupId, Guid memberId)
    {
        var group = await LoadGroupAsync(groupId);
        if (group == null)
            return (false, "Группа не найдена", null);

        var actor = group.Members.FirstOrDefault(member => member.UserId == currentUserId);
        if (actor == null)
            return (false, "Вы не состоите в этой группе", null);

        var target = group.Members.FirstOrDefault(member => member.UserId == memberId);
        if (target == null)
            return (false, "Участник группы не найден", null);

        if (target.UserId == currentUserId)
            return (false, "Для выхода из группы используйте отдельное действие", null);

        if (target.Role == GroupRole.Owner)
            return (false, "Нельзя удалить владельца группы", null);

        var canRemove = actor.Role == GroupRole.Owner
                        || (actor.Role == GroupRole.Admin && target.Role == GroupRole.Member);

        if (!canRemove)
            return (false, "У вас нет прав удалить этого участника", null);

        _context.GroupMembers.Remove(target);
        await _context.SaveChangesAsync();

        if (target.User != null)
        {
            await _notificationService.CreateNotificationAsync(
                target.UserId,
                NotificationType.Info,
                "Вы исключены из группы",
                $"Вас удалили из группы «{group.Name}»",
                group.Id);
        }

        group.Members.Remove(target);
        return (true, string.Empty, ToGroupResponse(group));
    }

    public async Task<(bool IsSuccess, string ErrorMessage)> LeaveGroupAsync(Guid currentUserId, Guid groupId)
    {
        var membership = await _context.GroupMembers
            .Include(member => member.Group)
            .FirstOrDefaultAsync(member => member.GroupId == groupId && member.UserId == currentUserId);

        if (membership == null)
            return (false, "Вы не состоите в этой группе");

        if (membership.Role == GroupRole.Owner)
            return (false, "Нельзя покинуть группу владельцу");

        _context.GroupMembers.Remove(membership);
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

    private async Task<Group?> LoadGroupAsync(Guid groupId) =>
        await _context.Groups
            .Include(group => group.Members)
            .ThenInclude(member => member.User)
            .FirstOrDefaultAsync(group => group.Id == groupId);

    private static string GetRoleLabel(GroupRole role) => role switch
    {
        GroupRole.Admin => "администратор",
        GroupRole.Member => "участник",
        _ => "участник"
    };
}
