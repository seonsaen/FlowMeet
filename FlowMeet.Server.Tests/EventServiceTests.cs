using FlowMeet.Server.Models.DTOs;
using FlowMeet.Server.Models.Entities;
using FlowMeet.Server.Services;

namespace FlowMeet.Server.Tests;

public class EventServiceTests
{
    [Fact]
    public async Task GetUserScheduleAsync_ExcludesProposedMeetingsUntilConfirmed()
    {
        await using var context = TestDbFactory.CreateContext();
        var service = new EventService(context);
        var userId = Guid.NewGuid();
        var proposedMeetingId = Guid.NewGuid();
        var confirmedMeetingId = Guid.NewGuid();

        context.Users.AddRange(
            new User
            {
                Id = userId,
                Email = "user@example.com",
                PasswordHash = "hash",
                FirstName = "Test",
                LastName = "User"
            },
            new User
            {
                Id = Guid.NewGuid(),
                Email = "friend@example.com",
                PasswordHash = "hash",
                FirstName = "Friend",
                LastName = "User"
            });

        context.Meetings.AddRange(
            new Meeting
            {
                Id = proposedMeetingId,
                InitiatorId = userId,
                Title = "Pending",
                StartTime = new DateTime(2026, 5, 7, 12, 0, 0, DateTimeKind.Utc),
                Duration = TimeSpan.FromHours(1),
                Status = MeetingStatus.Proposed
            },
            new Meeting
            {
                Id = confirmedMeetingId,
                InitiatorId = userId,
                Title = "Confirmed",
                StartTime = new DateTime(2026, 5, 8, 12, 0, 0, DateTimeKind.Utc),
                Duration = TimeSpan.FromHours(1),
                Status = MeetingStatus.Confirmed
            });

        await context.SaveChangesAsync();

        var schedule = await service.GetUserScheduleAsync(userId);

        Assert.DoesNotContain(schedule, item => item.Id == proposedMeetingId);
        Assert.Contains(schedule, item => item.Id == confirmedMeetingId && item.Source == "meeting");
    }

    [Fact]
    public async Task OverrideBaseOccurrenceAsync_CreatesExceptionAndOneOffEvent()
    {
        await using var context = TestDbFactory.CreateContext();
        var service = new EventService(context);
        var userId = Guid.NewGuid();
        var baseEntryId = Guid.NewGuid();
        var occurrenceDate = new DateOnly(2026, 5, 6);

        context.Users.Add(new User
        {
            Id = userId,
            Email = "user@example.com",
            PasswordHash = "hash",
            FirstName = "Test",
            LastName = "User"
        });

        context.BaseScheduleEntries.Add(new BaseScheduleEntry
        {
            Id = baseEntryId,
            UserId = userId,
            DayOfWeek = (int)occurrenceDate.DayOfWeek,
            Title = "Базовый блок",
            StartTime = TimeSpan.FromHours(10),
            EndTime = TimeSpan.FromHours(11),
            Type = EventType.Mandatory
        });

        await context.SaveChangesAsync();

        var result = await service.OverrideBaseOccurrenceAsync(userId, new OverrideBaseScheduleOccurrenceRequest
        {
            BaseScheduleEntryId = baseEntryId,
            OccurrenceDate = occurrenceDate,
            Title = "Измененный блок",
            StartTime = new DateTime(2026, 5, 6, 12, 0, 0, DateTimeKind.Utc),
            EndTime = new DateTime(2026, 5, 6, 13, 0, 0, DateTimeKind.Utc),
            Type = EventType.Flexible
        });

        Assert.True(result.IsSuccess);
        Assert.Single(context.Events);
        Assert.Single(context.BaseScheduleOccurrenceExceptions);
        Assert.Equal(context.Events.Single().Id, context.BaseScheduleOccurrenceExceptions.Single().OverrideEventId);
    }

    [Fact]
    public async Task CancelBaseOccurrenceAsync_RemovesOverrideEventAndKeepsException()
    {
        await using var context = TestDbFactory.CreateContext();
        var service = new EventService(context);
        var userId = Guid.NewGuid();
        var baseEntryId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var occurrenceDate = new DateOnly(2026, 5, 6);

        context.Users.Add(new User
        {
            Id = userId,
            Email = "user@example.com",
            PasswordHash = "hash",
            FirstName = "Test",
            LastName = "User"
        });

        context.BaseScheduleEntries.Add(new BaseScheduleEntry
        {
            Id = baseEntryId,
            UserId = userId,
            DayOfWeek = (int)occurrenceDate.DayOfWeek,
            Title = "Базовый блок",
            StartTime = TimeSpan.FromHours(10),
            EndTime = TimeSpan.FromHours(11),
            Type = EventType.Mandatory
        });

        context.Events.Add(new Event
        {
            Id = eventId,
            UserId = userId,
            Title = "Разовая перезапись",
            StartTime = new DateTime(2026, 5, 6, 12, 0, 0, DateTimeKind.Utc),
            EndTime = new DateTime(2026, 5, 6, 13, 0, 0, DateTimeKind.Utc),
            Type = EventType.Flexible
        });

        context.BaseScheduleOccurrenceExceptions.Add(new BaseScheduleOccurrenceException
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            BaseScheduleEntryId = baseEntryId,
            Date = occurrenceDate,
            OverrideEventId = eventId
        });

        await context.SaveChangesAsync();

        var result = await service.CancelBaseOccurrenceAsync(userId, new CancelBaseScheduleOccurrenceRequest
        {
            BaseScheduleEntryId = baseEntryId,
            OccurrenceDate = occurrenceDate
        });

        Assert.True(result.IsSuccess);
        Assert.Empty(context.Events);
        Assert.Single(context.BaseScheduleOccurrenceExceptions);
        Assert.Null(context.BaseScheduleOccurrenceExceptions.Single().OverrideEventId);
    }
}
