using FlowMeet.Server.Models.Entities;
using FlowMeet.Server.Services;

namespace FlowMeet.Server.Tests;

public class PlanningServiceTests
{
    [Fact]
    public async Task FindGroupSlotsAsync_WithNonPositiveDuration_ReturnsValidationError()
    {
        await using var context = TestDbFactory.CreateContext();
        var currentUserId = Guid.NewGuid();
        context.Users.Add(CreateUser(currentUserId));
        await context.SaveChangesAsync();

        var service = new PlanningService(context, new StubUserStateService());
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(2);

        var result = await service.FindGroupSlotsAsync(currentUserId, new List<Guid>(), startDate, 0);

        Assert.False(result.IsSuccess);
        Assert.Equal("Длительность встречи должна быть положительной", result.ErrorMessage);
        Assert.Empty(result.Slots);
    }

    [Fact]
    public async Task FindGroupSlotsAsync_WithUnauthorizedParticipant_ReturnsAccessError()
    {
        await using var context = TestDbFactory.CreateContext();
        var currentUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        context.Users.AddRange(
            CreateUser(currentUserId),
            CreateUser(otherUserId));
        await context.SaveChangesAsync();

        var service = new PlanningService(context, new StubUserStateService());
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(2);

        var result = await service.FindGroupSlotsAsync(currentUserId, new List<Guid> { otherUserId }, startDate, 60);

        Assert.False(result.IsSuccess);
        Assert.Equal("Можно планировать только с друзьями или участниками общих групп", result.ErrorMessage);
        Assert.Empty(result.Slots);
    }

    [Fact]
    public async Task FindGroupSlotsAsync_WithAcceptedFriend_ReturnsPreferredSlot()
    {
        await using var context = TestDbFactory.CreateContext();
        var currentUserId = Guid.NewGuid();
        var friendUserId = Guid.NewGuid();
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(2);

        context.Users.AddRange(
            CreateUser(currentUserId),
            CreateUser(friendUserId));
        context.Friendships.Add(new Friendship
        {
            RequesterId = currentUserId,
            AddresseeId = friendUserId,
            Status = FriendshipStatus.Accepted
        });
        await context.SaveChangesAsync();

        var service = new PlanningService(context, new StubUserStateService());

        var result = await service.FindGroupSlotsAsync(currentUserId, new List<Guid> { friendUserId }, startDate, 60);

        Assert.True(result.IsSuccess);
        Assert.NotEmpty(result.Slots);
        Assert.Equal(ToUtc(startDate, 11), result.Slots[0].StartTime);
        Assert.Equal("Optimal", result.Slots[0].Suitability);
    }

    [Fact]
    public async Task FindGroupSlotsAsync_WhenPreferredWindowBusy_FallsBackToCompromiseSlot()
    {
        await using var context = TestDbFactory.CreateContext();
        var currentUserId = Guid.NewGuid();
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(2);

        context.Users.Add(CreateUser(currentUserId, MeetingPreferencesJson("morning", "08:00", "15:00")));
        context.Events.AddRange(Enumerable.Range(0, 7).Select(dayOffset => new Event
        {
            Id = Guid.NewGuid(),
            UserId = currentUserId,
            Title = "Занят",
            StartTime = ToUtc(startDate.AddDays(dayOffset), 9),
            EndTime = ToUtc(startDate.AddDays(dayOffset), 13),
            Type = EventType.Mandatory
        }));
        await context.SaveChangesAsync();

        var service = new PlanningService(context, new StubUserStateService());

        var result = await service.FindGroupSlotsAsync(currentUserId, new List<Guid>(), startDate, 60);

        Assert.True(result.IsSuccess);
        Assert.NotEmpty(result.Slots);
        Assert.Equal(ToUtc(startDate, 8), result.Slots[0].StartTime);
        Assert.Equal("Compromise", result.Slots[0].Suitability);
        Assert.Contains("предпочтительного окна", result.Slots[0].Description);
    }

    [Fact]
    public async Task FindGroupSlotsAsync_WhenOnlyFlexibleConflictFits_MarksSlotAsRequiresMoving()
    {
        await using var context = TestDbFactory.CreateContext();
        var currentUserId = Guid.NewGuid();
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(2);

        context.Users.Add(CreateUser(currentUserId, MeetingPreferencesJson("morning", "08:00", "15:00")));
        context.Events.AddRange(Enumerable.Range(0, 7).SelectMany(dayOffset =>
        {
            var currentDate = startDate.AddDays(dayOffset);
            return new[]
            {
                new Event
                {
                    Id = Guid.NewGuid(),
                    UserId = currentUserId,
                    Title = "Тест",
                    StartTime = ToUtc(currentDate, 9),
                    EndTime = ToUtc(currentDate, 11),
                    Type = EventType.Mandatory
                },
                new Event
                {
                    Id = Guid.NewGuid(),
                    UserId = currentUserId,
                    Title = "Мягкий конфликт",
                    StartTime = ToUtc(currentDate, 11),
                    EndTime = ToUtc(currentDate, 12),
                    Type = EventType.Flexible
                },
                new Event
                {
                    Id = Guid.NewGuid(),
                    UserId = currentUserId,
                    Title = "Тест",
                    StartTime = ToUtc(currentDate, 12),
                    EndTime = ToUtc(currentDate, 13),
                    Type = EventType.Mandatory
                }
            };
        }));
        await context.SaveChangesAsync();

        var service = new PlanningService(context, new StubUserStateService());

        var result = await service.FindGroupSlotsAsync(currentUserId, new List<Guid>(), startDate, 60);

        Assert.True(result.IsSuccess);
        Assert.NotEmpty(result.Slots);
        Assert.Equal(ToUtc(startDate, 11), result.Slots[0].StartTime);
        Assert.Equal("RequiresMoving", result.Slots[0].Suitability);
        Assert.Contains("Потребуется перенос гибких дел", result.Slots[0].Description);
    }

    [Fact]
    public async Task FindGroupSlotsAsync_UsesProjectedResourcesWhenRankingCandidates()
    {
        await using var context = TestDbFactory.CreateContext();
        var currentUserId = Guid.NewGuid();
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(2);
        var bestSlotStart = ToUtc(startDate, 11, 30);

        context.Users.Add(CreateUser(currentUserId));
        await context.SaveChangesAsync();

        var userStateService = new StubUserStateService
        {
            ResourceFactory = (_, moment) =>
            {
                var normalized = moment.ToUniversalTime();
                if (normalized == bestSlotStart)
                    return 90;

                if (normalized == ToUtc(startDate, 11))
                    return 20;

                return 40;
            }
        };

        var service = new PlanningService(context, userStateService);

        var result = await service.FindGroupSlotsAsync(currentUserId, new List<Guid>(), startDate, 60);

        Assert.True(result.IsSuccess);
        Assert.NotEmpty(result.Slots);
        Assert.Equal(bestSlotStart, result.Slots[0].StartTime);
    }

    private static User CreateUser(Guid userId, string? settingsJson = null) => new()
    {
        Id = userId,
        Email = $"{userId:N}@example.com",
        PasswordHash = "hash",
        FirstName = "Test",
        LastName = "User",
        SettingsJson = settingsJson ?? "{}"
    };

    private static string MeetingPreferencesJson(string preset, string earliestTime, string latestTime)
        => $"{{\"meetingPreferences\":{{\"preset\":\"{preset}\",\"earliestTime\":\"{earliestTime}\",\"latestTime\":\"{latestTime}\"}}}}";

    private static DateTime ToUtc(DateOnly date, int hour, int minute = 0)
        => date.ToDateTime(new TimeOnly(hour, minute)).ToUniversalTime();
}
