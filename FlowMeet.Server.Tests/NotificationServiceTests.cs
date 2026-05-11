using FlowMeet.Server.Data;
using FlowMeet.Server.Models.Entities;
using FlowMeet.Server.Services;

namespace FlowMeet.Server.Tests;

public class NotificationServiceTests
{
    [Fact]
    public async Task CreateNotificationAsync_ImmediateNotificationIsVisibleImmediately()
    {
        await using var context = TestDbFactory.CreateContext();
        var emailService = new TestEmailService();
        var userId = Guid.NewGuid();
        context.Users.Add(CreateUser(userId));
        await context.SaveChangesAsync();

        var service = CreateService(context, emailService);

        var notification = await service.CreateNotificationAsync(userId, NotificationType.Info, "Новость", "Прямо сейчас");
        var visibleNotifications = await service.GetNotificationsAsync(userId, unreadOnly: false);
        var storedNotification = await context.Notifications.FindAsync(notification.Id);

        Assert.NotNull(storedNotification);
        Assert.NotNull(storedNotification!.DispatchedAt);
        Assert.Null(storedNotification.ScheduledFor);
        Assert.Single(visibleNotifications);
        Assert.Equal(notification.Id, visibleNotifications[0].Id);
    }

    [Fact]
    public async Task GetNotificationsAsync_UnreadOnlyFiltersHiddenAndReadItems()
    {
        await using var context = TestDbFactory.CreateContext();
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        context.Users.Add(CreateUser(userId));

        var scheduledUnread = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = NotificationType.Info,
            Title = "Будущее напоминание",
            Message = "Скоро",
            ScheduledFor = now.AddHours(1),
            CreatedAt = now.AddMinutes(-15),
            DispatchedAt = null
        };

        var immediateUnread = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = NotificationType.Info,
            Title = "Непрочитанное",
            Message = "Уже доступно",
            CreatedAt = now.AddMinutes(-20),
            DispatchedAt = now.AddMinutes(-20)
        };

        var immediateRead = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = NotificationType.Info,
            Title = "Прочитанное",
            Message = "Уже открывали",
            CreatedAt = now.AddMinutes(-10),
            DispatchedAt = now.AddMinutes(-10),
            ReadAt = now.AddMinutes(-5)
        };

        var hiddenDraft = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = NotificationType.Info,
            Title = "Скрытый черновик",
            Message = "Не должен попасть в выдачу",
            CreatedAt = now,
            DispatchedAt = null,
            ScheduledFor = null
        };

        context.Notifications.AddRange(scheduledUnread, immediateUnread, immediateRead, hiddenDraft);
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var allNotifications = await service.GetNotificationsAsync(userId, unreadOnly: false);
        var unreadNotifications = await service.GetNotificationsAsync(userId, unreadOnly: true);

        Assert.Equal(3, allNotifications.Count);
        Assert.Equal(scheduledUnread.Id, allNotifications[0].Id);
        Assert.Equal(2, unreadNotifications.Count);
        Assert.Equal(
            new[] { scheduledUnread.Id, immediateUnread.Id },
            unreadNotifications.Select(notification => notification.Id).ToArray());
    }

    [Fact]
    public async Task SyncMeetingNotificationsForMeetingAsync_CreatesRemindersForInitiatorAndAcceptedParticipants()
    {
        await using var context = TestDbFactory.CreateContext();
        var service = CreateService(context);
        var meetingId = Guid.NewGuid();
        var initiatorId = Guid.NewGuid();
        var acceptedUserId = Guid.NewGuid();
        var pendingUserId = Guid.NewGuid();
        var startTime = DateTime.UtcNow.AddHours(5);

        var initiator = CreateUser(initiatorId, firstName: "Иван", lastName: "Инициатор", settingsJson: """
            {"meetingReminderLeadMinutes":45}
            """);
        var acceptedUser = CreateUser(acceptedUserId, firstName: "Павел", lastName: "Участник", settingsJson: """
            {"meetingReminderLeadMinutes":30}
            """);
        var pendingUser = CreateUser(pendingUserId, firstName: "Петр", lastName: "Ожидает");

        context.Users.AddRange(initiator, acceptedUser, pendingUser);
        context.Meetings.Add(new Meeting
        {
            Id = meetingId,
            Title = "Созвон",
            Description = "Планирование",
            StartTime = startTime,
            Duration = TimeSpan.FromHours(1),
            InitiatorId = initiatorId,
            Status = MeetingStatus.Confirmed,
            Participants = new List<MeetingInvite>
            {
                new()
                {
                    MeetingId = meetingId,
                    UserId = acceptedUserId,
                    Status = ParticipantStatus.Accepted
                },
                new()
                {
                    MeetingId = meetingId,
                    UserId = pendingUserId,
                    Status = ParticipantStatus.Pending
                }
            }
        });
        await context.SaveChangesAsync();

        await service.SyncMeetingNotificationsForMeetingAsync(meetingId);

        var notifications = context.Notifications
            .OrderBy(notification => notification.UserId)
            .ToList();

        Assert.Equal(2, notifications.Count);
        Assert.Equal(
            new[] { acceptedUserId, initiatorId }.OrderBy(id => id).ToArray(),
            notifications.Select(notification => notification.UserId).ToArray());

        var initiatorNotification = notifications.Single(notification => notification.UserId == initiatorId);
        var acceptedNotification = notifications.Single(notification => notification.UserId == acceptedUserId);

        Assert.Equal(startTime.AddMinutes(-45), initiatorNotification.ScheduledFor);
        Assert.Equal(startTime.AddMinutes(-30), acceptedNotification.ScheduledFor);
        Assert.Contains("45", initiatorNotification.Title);
        Assert.Contains("Павел Участник", initiatorNotification.Message);
        Assert.Contains("Иван Инициатор", acceptedNotification.Message);
    }

    [Fact]
    public async Task SyncMeetingNotificationsForMeetingAsync_CancelledMeetingRemovesScheduledReminders()
    {
        await using var context = TestDbFactory.CreateContext();
        var service = CreateService(context);
        var meetingId = Guid.NewGuid();
        var initiatorId = Guid.NewGuid();

        context.Users.Add(CreateUser(initiatorId));
        context.Meetings.Add(new Meeting
        {
            Id = meetingId,
            Title = "Отмена",
            StartTime = DateTime.UtcNow.AddHours(3),
            Duration = TimeSpan.FromHours(1),
            InitiatorId = initiatorId,
            Status = MeetingStatus.Cancelled
        });
        context.Notifications.Add(new Notification
        {
            Id = Guid.NewGuid(),
            UserId = initiatorId,
            Type = NotificationType.Info,
            Title = "Старое напоминание",
            Message = "Устарело",
            RelatedEntityId = meetingId,
            ScheduledFor = DateTime.UtcNow.AddHours(2),
            CreatedAt = DateTime.UtcNow.AddHours(-1)
        });
        await context.SaveChangesAsync();

        await service.SyncMeetingNotificationsForMeetingAsync(meetingId);

        Assert.Empty(context.Notifications);
    }

    [Fact]
    public async Task SyncMeetingNotificationsForUserAsync_SyncsOnlyRelevantMeetings()
    {
        await using var context = TestDbFactory.CreateContext();
        var service = CreateService(context);
        var currentUserId = Guid.NewGuid();
        var teammateId = Guid.NewGuid();
        var outsiderId = Guid.NewGuid();
        var relevantMeetingId = Guid.NewGuid();
        var irrelevantMeetingId = Guid.NewGuid();

        context.Users.AddRange(
            CreateUser(currentUserId, firstName: "Текущий", lastName: "Пользователь"),
            CreateUser(teammateId, firstName: "Напарник", lastName: "Команды"),
            CreateUser(outsiderId, firstName: "Внешний", lastName: "Участник"));

        context.Meetings.Add(new Meeting
        {
            Id = relevantMeetingId,
            Title = "Актуальная встреча",
            StartTime = DateTime.UtcNow.AddHours(4),
            Duration = TimeSpan.FromHours(1),
            InitiatorId = currentUserId,
            Status = MeetingStatus.Confirmed,
            Participants = new List<MeetingInvite>
            {
                new()
                {
                    MeetingId = relevantMeetingId,
                    UserId = teammateId,
                    Status = ParticipantStatus.Accepted
                }
            }
        });

        context.Meetings.Add(new Meeting
        {
            Id = irrelevantMeetingId,
            Title = "Неактуальная встреча",
            StartTime = DateTime.UtcNow.AddHours(4),
            Duration = TimeSpan.FromHours(1),
            InitiatorId = outsiderId,
            Status = MeetingStatus.Confirmed,
            Participants = new List<MeetingInvite>
            {
                new()
                {
                    MeetingId = irrelevantMeetingId,
                    UserId = currentUserId,
                    Status = ParticipantStatus.Pending
                }
            }
        });
        await context.SaveChangesAsync();

        await service.SyncMeetingNotificationsForUserAsync(currentUserId);

        var notifications = context.Notifications.ToList();

        Assert.Equal(2, notifications.Count);
        Assert.All(notifications, notification => Assert.Equal(relevantMeetingId, notification.RelatedEntityId));
    }

    [Fact]
    public async Task DispatchDueScheduledNotificationsAsync_MakesNotificationVisibleAndSendsEmail()
    {
        await using var context = TestDbFactory.CreateContext();
        var emailService = new TestEmailService();
        var userId = Guid.NewGuid();
        const string email = "user@example.com";

        context.Users.Add(CreateUser(userId, email: email, settingsJson: """
            {"emailNotifications":true,"reminders":true}
            """));

        var reminder = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = NotificationType.Info,
            Title = "Скоро встреча",
            Message = "Через 10 минут",
            ScheduledFor = DateTime.UtcNow.AddMinutes(-5),
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
            DispatchedAt = null
        };

        context.Notifications.Add(reminder);
        await context.SaveChangesAsync();

        var service = CreateService(context, emailService);

        var dispatched = await service.DispatchDueScheduledNotificationsAsync();
        var notifications = await service.GetNotificationsAsync(userId, unreadOnly: false);
        var storedReminder = await context.Notifications.FindAsync(reminder.Id);

        Assert.Equal(1, dispatched);
        Assert.NotNull(storedReminder);
        Assert.NotNull(storedReminder!.DispatchedAt);
        Assert.Single(notifications);
        Assert.Single(emailService.SentEmails);
        Assert.Equal(email, emailService.SentEmails[0].ToEmail);
    }

    [Fact]
    public async Task DispatchDueScheduledNotificationsAsync_DispatchesWithoutEmailWhenRemindersDisabled()
    {
        await using var context = TestDbFactory.CreateContext();
        var emailService = new TestEmailService();
        var userId = Guid.NewGuid();

        context.Users.Add(CreateUser(userId, settingsJson: """
            {"emailNotifications":false,"reminders":true}
            """));
        context.Notifications.Add(new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = NotificationType.Info,
            Title = "Без письма",
            Message = "Только в приложении",
            ScheduledFor = DateTime.UtcNow.AddMinutes(-1),
            CreatedAt = DateTime.UtcNow.AddMinutes(-3)
        });
        await context.SaveChangesAsync();

        var service = CreateService(context, emailService);

        var dispatched = await service.DispatchDueScheduledNotificationsAsync();
        var storedNotification = Assert.Single(context.Notifications);

        Assert.Equal(1, dispatched);
        Assert.NotNull(storedNotification.DispatchedAt);
        Assert.Empty(emailService.SentEmails);
    }

    [Fact]
    public async Task MarkAsReadAsync_SetsTimestampForOwnerAndRejectsOthers()
    {
        await using var context = TestDbFactory.CreateContext();
        var ownerId = Guid.NewGuid();
        var strangerId = Guid.NewGuid();
        var notificationId = Guid.NewGuid();

        context.Users.AddRange(
            CreateUser(ownerId),
            CreateUser(strangerId, email: "other@example.com"));
        context.Notifications.Add(new Notification
        {
            Id = notificationId,
            UserId = ownerId,
            Type = NotificationType.Info,
            Title = "Нужно открыть",
            Message = "Прочтите это",
            CreatedAt = DateTime.UtcNow,
            DispatchedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var strangerResult = await service.MarkAsReadAsync(strangerId, notificationId);
        var beforeOwnerRead = await context.Notifications.FindAsync(notificationId);
        var readAtAfterStrangerAttempt = beforeOwnerRead?.ReadAt;
        var ownerResult = await service.MarkAsReadAsync(ownerId, notificationId);
        var storedNotification = await context.Notifications.FindAsync(notificationId);

        Assert.False(strangerResult.IsSuccess);
        Assert.Null(readAtAfterStrangerAttempt);
        Assert.True(ownerResult.IsSuccess);
        Assert.NotNull(storedNotification);
        Assert.NotNull(storedNotification!.ReadAt);
    }

    [Fact]
    public async Task DeleteNotificationAsync_RemovesOwnedNotificationAndRejectsOthers()
    {
        await using var context = TestDbFactory.CreateContext();
        var ownerId = Guid.NewGuid();
        var strangerId = Guid.NewGuid();
        var notificationId = Guid.NewGuid();

        context.Users.AddRange(
            CreateUser(ownerId),
            CreateUser(strangerId, email: "other@example.com"));
        context.Notifications.Add(new Notification
        {
            Id = notificationId,
            UserId = ownerId,
            Type = NotificationType.Info,
            Title = "Удаляемое",
            Message = "Будет удалено",
            CreatedAt = DateTime.UtcNow,
            DispatchedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var strangerResult = await service.DeleteNotificationAsync(strangerId, notificationId);
        var afterFailedDelete = await context.Notifications.FindAsync(notificationId);
        var ownerResult = await service.DeleteNotificationAsync(ownerId, notificationId);
        var afterOwnerDelete = await context.Notifications.FindAsync(notificationId);

        Assert.False(strangerResult.IsSuccess);
        Assert.NotNull(afterFailedDelete);
        Assert.True(ownerResult.IsSuccess);
        Assert.Null(afterOwnerDelete);
    }

    private static NotificationService CreateService(AppDbContext context, TestEmailService? emailService = null)
        => new(context, emailService ?? new TestEmailService());

    private static User CreateUser(
        Guid userId,
        string? email = null,
        string firstName = "Test",
        string lastName = "User",
        string settingsJson = "{}") => new()
    {
        Id = userId,
        Email = email ?? $"{userId:N}@example.com",
        PasswordHash = "hash",
        FirstName = firstName,
        LastName = lastName,
        SettingsJson = settingsJson
    };
}
