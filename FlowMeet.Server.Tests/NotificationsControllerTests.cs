using FlowMeet.Server.Controllers;
using FlowMeet.Server.Models.DTOs;
using FlowMeet.Server.Models.Entities;
using FlowMeet.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace FlowMeet.Server.Tests;

public class NotificationsControllerTests
{
    [Fact]
    public async Task GetMyNotifications_WithoutUser_ReturnsUnauthorized()
    {
        var service = new FakeNotificationService();
        var controller = new NotificationsController(service);
        ControllerTestHelper.SetUser(controller);

        var result = await controller.GetMyNotifications(unreadOnly: true);

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetMyNotifications_PassesUnreadFlagAndReturnsOk()
    {
        var userId = Guid.NewGuid();
        var service = new FakeNotificationService
        {
            Notifications = new List<NotificationDto>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Title = "Новое уведомление",
                    Message = "Текст",
                    Type = NotificationType.Info
                }
            }
        };
        var controller = new NotificationsController(service);
        ControllerTestHelper.SetUser(controller, userId);

        var result = await controller.GetMyNotifications(unreadOnly: true);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var notifications = Assert.IsType<List<NotificationDto>>(ok.Value);

        Assert.Equal(userId, service.LastNotificationUserId);
        Assert.True(service.LastUnreadOnly);
        Assert.Single(notifications);
    }

    [Fact]
    public async Task MarkAsRead_ServiceErrorMapsToNotFound()
    {
        var service = new FakeNotificationService
        {
            MarkAsReadResult = (false, "Уведомление не найдено")
        };
        var controller = new NotificationsController(service);
        ControllerTestHelper.SetUser(controller, Guid.NewGuid());

        var result = await controller.MarkAsRead(Guid.NewGuid());

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task DeleteNotification_SuccessReturnsOkMessage()
    {
        var userId = Guid.NewGuid();
        var notificationId = Guid.NewGuid();
        var service = new FakeNotificationService
        {
            DeleteResult = (true, string.Empty)
        };
        var controller = new NotificationsController(service);
        ControllerTestHelper.SetUser(controller, userId);

        var result = await controller.DeleteNotification(notificationId);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(userId, service.LastDeleteUserId);
        Assert.Equal(notificationId, service.LastDeleteNotificationId);
        Assert.Equal("Уведомление удалено", ControllerTestHelper.GetValue<string>(ok.Value!, "message"));
    }

    private sealed class FakeNotificationService : INotificationService
    {
        public List<NotificationDto> Notifications { get; set; } = new();
        public (bool IsSuccess, string ErrorMessage) MarkAsReadResult { get; set; } = (true, string.Empty);
        public (bool IsSuccess, string ErrorMessage) DeleteResult { get; set; } = (true, string.Empty);
        public Guid LastNotificationUserId { get; private set; }
        public bool LastUnreadOnly { get; private set; }
        public Guid LastMarkAsReadUserId { get; private set; }
        public Guid LastMarkAsReadNotificationId { get; private set; }
        public Guid LastDeleteUserId { get; private set; }
        public Guid LastDeleteNotificationId { get; private set; }

        public Task<List<NotificationDto>> GetNotificationsAsync(Guid userId, bool unreadOnly)
        {
            LastNotificationUserId = userId;
            LastUnreadOnly = unreadOnly;
            return Task.FromResult(Notifications);
        }

        public Task<NotificationDto> CreateNotificationAsync(Guid userId, NotificationType type, string title, string message, Guid? relatedEntityId = null, DateTime? scheduledFor = null)
            => Task.FromResult(new NotificationDto());

        public Task SyncMeetingNotificationsForMeetingAsync(Guid meetingId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SyncMeetingNotificationsForUserAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<int> DispatchDueScheduledNotificationsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<(bool IsSuccess, string ErrorMessage)> MarkAsReadAsync(Guid userId, Guid notificationId)
        {
            LastMarkAsReadUserId = userId;
            LastMarkAsReadNotificationId = notificationId;
            return Task.FromResult(MarkAsReadResult);
        }

        public Task<(bool IsSuccess, string ErrorMessage)> DeleteNotificationAsync(Guid userId, Guid notificationId)
        {
            LastDeleteUserId = userId;
            LastDeleteNotificationId = notificationId;
            return Task.FromResult(DeleteResult);
        }
    }
}
