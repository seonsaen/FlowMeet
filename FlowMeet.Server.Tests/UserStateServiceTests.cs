using FlowMeet.Server.Models.DTOs;
using FlowMeet.Server.Models.Entities;
using FlowMeet.Server.Services;

namespace FlowMeet.Server.Tests;

public class UserStateServiceTests
{
    [Fact]
    public async Task GetResourceStatusAsync_WithoutSavedState_ReturnsDefaultsAndHighResource()
    {
        await using var context = TestDbFactory.CreateContext();
        var service = new UserStateService(context);

        var result = await service.GetResourceStatusAsync(Guid.NewGuid());

        Assert.Equal(85, result.ResourceLevel);
        Assert.Equal(85, result.RawBalance);
        Assert.Equal("Высокий ресурс, можно планировать активные встречи", result.StatusMessage);
        Assert.Equal(3, result.MoodLevel);
        Assert.Equal(SleepQuality.Normal, result.SleepQuality);
        Assert.Equal(BackgroundLoadLevel.Calm, result.BackgroundLoadLevel);
    }

    [Fact]
    public async Task SetMoodAsync_FirstPoorSleepInputLimitsMorningResourceWithoutExtraSleepAdjustment()
    {
        await using var context = TestDbFactory.CreateContext();
        var service = new UserStateService(context);
        var userId = Guid.NewGuid();

        context.Users.Add(CreateUser(userId));
        await context.SaveChangesAsync();

        var result = await service.SetMoodAsync(userId, new MoodRequest
        {
            MoodLevel = 3,
            SleepQuality = SleepQuality.Poor,
            BackgroundLoadLevel = BackgroundLoadLevel.Calm
        });

        var storedState = Assert.Single(context.UserStates);

        Assert.Equal(65, result.ResourceLevel);
        Assert.Equal(65, storedState.RawBalance);
        Assert.Equal(65, storedState.ResourceLevel);
    }

    [Theory]
    [InlineData(SleepQuality.Poor, 65)]
    [InlineData(SleepQuality.Normal, 85)]
    [InlineData(SleepQuality.Good, 100)]
    public async Task SetMoodAsync_FirstSleepInputUsesDistinctMorningResourceLevels(SleepQuality sleepQuality, int expectedResource)
    {
        await using var context = TestDbFactory.CreateContext();
        var service = new UserStateService(context);
        var userId = Guid.NewGuid();

        context.Users.Add(CreateUser(userId));
        await context.SaveChangesAsync();

        var result = await service.SetMoodAsync(userId, new MoodRequest
        {
            MoodLevel = 3,
            SleepQuality = sleepQuality,
            BackgroundLoadLevel = BackgroundLoadLevel.Calm
        });

        Assert.Equal(expectedResource, result.ResourceLevel);
    }

    [Fact]
    public async Task SetMoodAsync_ExistingStateIsUpdatedWithoutCreatingDuplicate()
    {
        await using var context = TestDbFactory.CreateContext();
        var service = new UserStateService(context);
        var userId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        context.Users.Add(CreateUser(userId));
        context.UserStates.Add(new UserState
        {
            UserId = userId,
            Date = today,
            MoodLevel = 5,
            ResourceLevel = 100,
            RawBalance = 100,
            SleepQuality = SleepQuality.Good,
            BackgroundLoadLevel = BackgroundLoadLevel.Calm
        });
        await context.SaveChangesAsync();

        var result = await service.SetMoodAsync(userId, new MoodRequest
        {
            MoodLevel = 2,
            SleepQuality = SleepQuality.Poor,
            BackgroundLoadLevel = BackgroundLoadLevel.Heavy
        });

        var storedState = Assert.Single(context.UserStates);

        Assert.Equal("Состояние дня сохранено", result.Message);
        Assert.Equal(2, storedState.MoodLevel);
        Assert.Equal(SleepQuality.Poor, storedState.SleepQuality);
        Assert.Equal(BackgroundLoadLevel.Heavy, storedState.BackgroundLoadLevel);
        Assert.Equal(result.ResourceLevel, storedState.ResourceLevel);
        Assert.InRange(storedState.ResourceLevel, 0, 100);
    }

    [Fact]
    public async Task GetProjectedResourceAsync_BaseScheduleEntryReducesProjection()
    {
        await using var context = TestDbFactory.CreateContext();
        var service = new UserStateService(context);
        var userId = Guid.NewGuid();
        var evaluationMoment = new DateTime(2026, 5, 4, 13, 0, 0, DateTimeKind.Utc);
        var targetDate = DateOnly.FromDateTime(evaluationMoment);

        context.Users.Add(CreateUser(userId));
        context.BaseScheduleEntries.Add(new BaseScheduleEntry
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            DayOfWeek = (int)targetDate.DayOfWeek,
            Title = "Лекция",
            StartTime = TimeSpan.FromHours(10),
            EndTime = TimeSpan.FromHours(12),
            Type = EventType.Mandatory,
            EffectiveFromDate = targetDate
        });
        await context.SaveChangesAsync();

        var result = await service.GetProjectedResourceAsync(userId, evaluationMoment);

        Assert.Equal(57, result);
    }

    [Fact]
    public async Task GetProjectedResourceAsync_BaseScheduleExceptionSkipsOccurrence()
    {
        await using var context = TestDbFactory.CreateContext();
        var service = new UserStateService(context);
        var userId = Guid.NewGuid();
        var evaluationMoment = new DateTime(2026, 5, 4, 13, 0, 0, DateTimeKind.Utc);
        var targetDate = DateOnly.FromDateTime(evaluationMoment);
        var entryId = Guid.NewGuid();

        context.Users.Add(CreateUser(userId));
        context.BaseScheduleEntries.Add(new BaseScheduleEntry
        {
            Id = entryId,
            UserId = userId,
            DayOfWeek = (int)targetDate.DayOfWeek,
            Title = "Лекция",
            StartTime = TimeSpan.FromHours(10),
            EndTime = TimeSpan.FromHours(12),
            Type = EventType.Mandatory,
            EffectiveFromDate = targetDate
        });
        context.BaseScheduleOccurrenceExceptions.Add(new BaseScheduleOccurrenceException
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            BaseScheduleEntryId = entryId,
            Date = targetDate
        });
        await context.SaveChangesAsync();

        var result = await service.GetProjectedResourceAsync(userId, evaluationMoment);

        Assert.Equal(85, result);
    }

    [Fact]
    public async Task GetProjectedResourceAsync_OnlyAcceptedConfirmedMeetingsAffectProjection()
    {
        await using var context = TestDbFactory.CreateContext();
        var service = new UserStateService(context);
        var userId = Guid.NewGuid();
        var evaluationMoment = new DateTime(2026, 5, 4, 12, 0, 0, DateTimeKind.Utc);
        var acceptedMeetingId = Guid.NewGuid();
        var pendingMeetingId = Guid.NewGuid();

        context.Users.Add(CreateUser(userId));
        context.Meetings.Add(new Meeting
        {
            Id = acceptedMeetingId,
            Title = "Accepted meeting",
            StartTime = new DateTime(2026, 5, 4, 9, 0, 0, DateTimeKind.Utc),
            Duration = TimeSpan.FromHours(1),
            InitiatorId = Guid.NewGuid(),
            Status = MeetingStatus.Confirmed,
            Participants = new List<MeetingInvite>
            {
                new()
                {
                    MeetingId = acceptedMeetingId,
                    UserId = userId,
                    Status = ParticipantStatus.Accepted
                }
            }
        });
        context.Meetings.Add(new Meeting
        {
            Id = pendingMeetingId,
            Title = "Pending meeting",
            StartTime = new DateTime(2026, 5, 4, 10, 0, 0, DateTimeKind.Utc),
            Duration = TimeSpan.FromHours(1),
            InitiatorId = Guid.NewGuid(),
            Status = MeetingStatus.Confirmed,
            Participants = new List<MeetingInvite>
            {
                new()
                {
                    MeetingId = pendingMeetingId,
                    UserId = userId,
                    Status = ParticipantStatus.Pending
                }
            }
        });
        await context.SaveChangesAsync();

        var result = await service.GetProjectedResourceAsync(userId, evaluationMoment);

        Assert.Equal(71, result);
    }

    private static User CreateUser(Guid userId) => new()
    {
        Id = userId,
        Email = $"{userId:N}@example.com",
        PasswordHash = "hash",
        FirstName = "Test",
        LastName = "User"
    };
}
