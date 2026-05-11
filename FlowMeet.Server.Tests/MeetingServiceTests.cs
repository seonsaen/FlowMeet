using FlowMeet.Server.Models.DTOs;
using FlowMeet.Server.Models.Entities;
using FlowMeet.Server.Services;

namespace FlowMeet.Server.Tests;

public class MeetingServiceTests
{
    [Fact]
    public async Task GetIncomingInvitesAsync_ExpiresPastDuePendingMeetings()
    {
        await using var context = TestDbFactory.CreateContext();
        var notifications = new RecordingNotificationService();
        var service = new MeetingService(context, notifications);
        var organizerId = Guid.NewGuid();
        var inviteeId = Guid.NewGuid();
        var meetingId = Guid.NewGuid();

        context.Users.AddRange(
            new User { Id = organizerId, Email = "organizer@example.com", PasswordHash = "hash", FirstName = "Org", LastName = "User" },
            new User { Id = inviteeId, Email = "invitee@example.com", PasswordHash = "hash", FirstName = "Invitee", LastName = "User" });

        context.Meetings.Add(new Meeting
        {
            Id = meetingId,
            InitiatorId = organizerId,
            Title = "Просроченная встреча",
            StartTime = DateTime.UtcNow.AddHours(-2),
            Duration = TimeSpan.FromHours(1),
            Status = MeetingStatus.Proposed
        });

        context.Invites.Add(new MeetingInvite
        {
            MeetingId = meetingId,
            UserId = inviteeId,
            Status = ParticipantStatus.Pending
        });

        await context.SaveChangesAsync();

        var incomingInvites = await service.GetIncomingInvitesAsync(inviteeId);

        Assert.Empty(incomingInvites);
        Assert.Equal(MeetingStatus.Cancelled, context.Meetings.Single().Status);
        Assert.Equal(ParticipantStatus.Declined, context.Invites.Single().Status);
        Assert.Equal(2, notifications.CreatedNotifications.Count);
    }

    [Fact]
    public async Task GetOutgoingInvitesAsync_ReturnsOnlyProposedMeetings()
    {
        await using var context = TestDbFactory.CreateContext();
        var notifications = new RecordingNotificationService();
        var service = new MeetingService(context, notifications);
        var organizerId = Guid.NewGuid();
        var pendingUserId = Guid.NewGuid();
        var acceptedUserId = Guid.NewGuid();
        var proposedMeetingId = Guid.NewGuid();

        context.Users.AddRange(
            new User { Id = organizerId, Email = "organizer@example.com", PasswordHash = "hash", FirstName = "Org", LastName = "User" },
            new User { Id = pendingUserId, Email = "pending@example.com", PasswordHash = "hash", FirstName = "Pending", LastName = "User" },
            new User { Id = acceptedUserId, Email = "accepted@example.com", PasswordHash = "hash", FirstName = "Accepted", LastName = "User" });

        context.Meetings.AddRange(
            new Meeting
            {
                Id = proposedMeetingId,
                InitiatorId = organizerId,
                Title = "Pending meeting",
                StartTime = DateTime.UtcNow.AddDays(1),
                Duration = TimeSpan.FromHours(1),
                Status = MeetingStatus.Proposed
            },
            new Meeting
            {
                Id = Guid.NewGuid(),
                InitiatorId = organizerId,
                Title = "Confirmed meeting",
                StartTime = DateTime.UtcNow.AddDays(2),
                Duration = TimeSpan.FromHours(1),
                Status = MeetingStatus.Confirmed
            });

        context.Invites.AddRange(
            new MeetingInvite { MeetingId = proposedMeetingId, UserId = pendingUserId, Status = ParticipantStatus.Pending },
            new MeetingInvite { MeetingId = proposedMeetingId, UserId = acceptedUserId, Status = ParticipantStatus.Accepted });

        await context.SaveChangesAsync();

        var result = await service.GetOutgoingInvitesAsync(organizerId);

        var outgoing = Assert.Single(result);
        Assert.Equal(proposedMeetingId, outgoing.MeetingId);
        Assert.Equal(1, outgoing.PendingParticipantsCount);
        Assert.Equal(1, outgoing.AcceptedParticipantsCount);
        Assert.Equal(2, outgoing.TotalParticipantsCount);
        Assert.Contains("Pending User", outgoing.PendingParticipantNames);
    }

    [Fact]
    public async Task GetMeetingHistoryAsync_ReturnsOnlyPastConfirmedMeetingsForUser()
    {
        await using var context = TestDbFactory.CreateContext();
        var notifications = new RecordingNotificationService();
        var service = new MeetingService(context, notifications);
        var currentUserId = Guid.NewGuid();
        var teammateId = Guid.NewGuid();
        var pastMeetingId = Guid.NewGuid();

        context.Users.AddRange(
            new User { Id = currentUserId, Email = "current@example.com", PasswordHash = "hash", FirstName = "Current", LastName = "User" },
            new User { Id = teammateId, Email = "teammate@example.com", PasswordHash = "hash", FirstName = "Team", LastName = "Mate" });

        context.Meetings.AddRange(
            new Meeting
            {
                Id = pastMeetingId,
                InitiatorId = currentUserId,
                Title = "Past confirmed",
                StartTime = DateTime.UtcNow.AddDays(-3),
                Duration = TimeSpan.FromHours(1),
                Status = MeetingStatus.Confirmed,
                Participants = new List<MeetingInvite>
                {
                    new() { MeetingId = pastMeetingId, UserId = teammateId, Status = ParticipantStatus.Accepted }
                }
            },
            new Meeting
            {
                Id = Guid.NewGuid(),
                InitiatorId = currentUserId,
                Title = "Future confirmed",
                StartTime = DateTime.UtcNow.AddDays(3),
                Duration = TimeSpan.FromHours(1),
                Status = MeetingStatus.Confirmed
            },
            new Meeting
            {
                Id = Guid.NewGuid(),
                InitiatorId = currentUserId,
                Title = "Past cancelled",
                StartTime = DateTime.UtcNow.AddDays(-2),
                Duration = TimeSpan.FromHours(1),
                Status = MeetingStatus.Cancelled
            });

        await context.SaveChangesAsync();

        var result = await service.GetMeetingHistoryAsync(currentUserId);

        var meeting = Assert.Single(result);
        Assert.Equal(pastMeetingId, meeting.MeetingId);
        Assert.False(meeting.CanEdit);
        Assert.Contains("Team Mate", meeting.ParticipantNames);
    }
}
