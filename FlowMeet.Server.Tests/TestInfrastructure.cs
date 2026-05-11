using FlowMeet.Server.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using FlowMeet.Server.Models.DTOs;
using FlowMeet.Server.Models.Entities;
using FlowMeet.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using System.Reflection;
using System.Security.Claims;

namespace FlowMeet.Server.Tests;

internal static class TestDbFactory
{
    public static AppDbContext CreateContext(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString("N"))
            .Options;

        var context = new AppDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }
}

internal sealed class TestEmailService : IEmailService
{
    public List<SentEmail> SentEmails { get; } = new();

    public Task<bool> SendAsync(string toEmail, string subject, string textBody, CancellationToken cancellationToken = default)
    {
        SentEmails.Add(new SentEmail(toEmail, subject, textBody));
        return Task.FromResult(true);
    }
}

internal sealed record SentEmail(string ToEmail, string Subject, string TextBody);

internal sealed class RecordingNotificationService : INotificationService
{
    public List<(Guid UserId, NotificationType Type, string Title, string Message, Guid? RelatedEntityId, DateTime? ScheduledFor)> CreatedNotifications { get; } = new();
    public List<Guid> SyncedMeetingNotificationUserIds { get; } = new();
    public List<Guid> SyncedMeetingNotificationMeetingIds { get; } = new();

    public Task<List<NotificationDto>> GetNotificationsAsync(Guid userId, bool unreadOnly) => Task.FromResult(new List<NotificationDto>());

    public Task<NotificationDto> CreateNotificationAsync(Guid userId, NotificationType type, string title, string message, Guid? relatedEntityId = null, DateTime? scheduledFor = null)
    {
        CreatedNotifications.Add((userId, type, title, message, relatedEntityId, scheduledFor));

        return Task.FromResult(new NotificationDto
        {
            Id = Guid.NewGuid(),
            Type = type,
            Title = title,
            Message = message,
            RelatedEntityId = relatedEntityId,
            ScheduledFor = scheduledFor,
            CreatedAt = DateTime.UtcNow
        });
    }

    public Task SyncMeetingNotificationsForMeetingAsync(Guid meetingId, CancellationToken cancellationToken = default)
    {
        SyncedMeetingNotificationMeetingIds.Add(meetingId);
        return Task.CompletedTask;
    }

    public Task SyncMeetingNotificationsForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        SyncedMeetingNotificationUserIds.Add(userId);
        return Task.CompletedTask;
    }

    public Task<int> DispatchDueScheduledNotificationsAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);

    public Task<(bool IsSuccess, string ErrorMessage)> MarkAsReadAsync(Guid userId, Guid notificationId)
        => Task.FromResult((true, string.Empty));

    public Task<(bool IsSuccess, string ErrorMessage)> DeleteNotificationAsync(Guid userId, Guid notificationId)
        => Task.FromResult((true, string.Empty));
}

internal sealed class StubPlanningService : IPlanningService
{
    public List<TimeSlotDto> Slots { get; set; } = new();

    public Task<(bool IsSuccess, string ErrorMessage, List<TimeSlotDto> Slots)> FindGroupSlotsAsync(Guid currentUserId, List<Guid> participantIds, DateOnly startDate, int durationMinutes)
        => Task.FromResult((true, string.Empty, Slots));
}

internal sealed class StubUserStateService : IUserStateService
{
    public int DefaultResource { get; set; } = 80;
    public Func<Guid, DateTime, int>? ResourceFactory { get; set; }

    public Task<(int ResourceLevel, string Message)> SetMoodAsync(Guid userId, MoodRequest request)
        => Task.FromResult((DefaultResource, "stub"));

    public Task<ResourceResponse> GetResourceStatusAsync(Guid userId)
        => Task.FromResult(new ResourceResponse
        {
            ResourceLevel = DefaultResource,
            RawBalance = DefaultResource,
            MoodLevel = 3,
            SleepQuality = SleepQuality.Normal,
            BackgroundLoadLevel = BackgroundLoadLevel.Calm,
            StatusMessage = "stub"
        });

    public Task<int> GetProjectedResourceAsync(Guid userId, DateTime momentUtc)
        => Task.FromResult(GetResource(userId, momentUtc));

    public Task<Dictionary<DateTime, int>> GetProjectedResourcesAsync(Guid userId, IReadOnlyCollection<DateTime> momentsUtc)
        => Task.FromResult(momentsUtc
            .Select(moment => moment.ToUniversalTime())
            .Distinct()
            .ToDictionary(moment => moment, moment => GetResource(userId, moment)));

    private int GetResource(Guid userId, DateTime momentUtc)
        => ResourceFactory?.Invoke(userId, momentUtc.ToUniversalTime()) ?? DefaultResource;
}

internal sealed class FakeHostEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; } = Environments.Development;
    public string ApplicationName { get; set; } = "FlowMeet.Tests";
    public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}

internal static class ControllerTestHelper
{
    public static void SetUser(ControllerBase controller, Guid? userId = null)
    {
        var httpContext = new DefaultHttpContext();
        if (userId.HasValue)
        {
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()) },
                authenticationType: "Test"));
        }

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    public static T GetValue<T>(object source, string propertyName)
    {
        var property = source.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);

        Assert.NotNull(property);
        var value = property!.GetValue(source);
        Assert.NotNull(value);
        return Assert.IsType<T>(value);
    }
}
