using FlowMeet.Server.Controllers;
using FlowMeet.Server.Models.DTOs;
using FlowMeet.Server.Models.Entities;
using FlowMeet.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace FlowMeet.Server.Tests;

public class EventControllerTests
{
    [Fact]
    public async Task GetUserSchedule_WithoutUser_ReturnsUnauthorized()
    {
        var service = new FakeEventService();
        var controller = new EventController(service);
        ControllerTestHelper.SetUser(controller);

        var result = await controller.GetUserSchedule();

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetUserSchedule_ReturnsOkList()
    {
        var userId = Guid.NewGuid();
        var service = new FakeEventService
        {
            Schedule = new List<EventResponse>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Title = "Лекция",
                    StartTime = new DateTime(2026, 5, 10, 10, 0, 0, DateTimeKind.Utc),
                    EndTime = new DateTime(2026, 5, 10, 11, 0, 0, DateTimeKind.Utc),
                    Type = "Mandatory"
                }
            }
        };
        var controller = new EventController(service);
        ControllerTestHelper.SetUser(controller, userId);

        var result = await controller.GetUserSchedule();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var schedule = Assert.IsType<List<EventResponse>>(ok.Value);

        Assert.Equal(userId, service.LastScheduleUserId);
        Assert.Single(schedule);
    }

    [Fact]
    public async Task CreateEvent_ReturnsOkMessageAndEventId()
    {
        var userId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var service = new FakeEventService
        {
            CreateResult = (true, string.Empty, eventId)
        };
        var controller = new EventController(service);
        ControllerTestHelper.SetUser(controller, userId);

        var request = new CreateEventRequest
        {
            Title = "Учеба",
            StartTime = new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc),
            EndTime = new DateTime(2026, 5, 10, 13, 0, 0, DateTimeKind.Utc),
            Type = EventType.Mandatory
        };

        var result = await controller.CreateEvent(request);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(userId, service.LastCreateUserId);
        Assert.Same(request, service.LastCreateRequest);
        Assert.Equal(eventId, ControllerTestHelper.GetValue<Guid>(ok.Value!, "eventId"));
    }

    [Fact]
    public async Task UpdateEvent_ServiceErrorMapsToForbidden()
    {
        var service = new FakeEventService
        {
            UpdateResult = (false, "У вас нет прав редактировать это событие")
        };
        var controller = new EventController(service);
        ControllerTestHelper.SetUser(controller, Guid.NewGuid());

        var result = await controller.UpdateEvent(Guid.NewGuid(), new UpdateEventRequest
        {
            Title = "Новое",
            StartTime = new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc),
            EndTime = new DateTime(2026, 5, 10, 13, 0, 0, DateTimeKind.Utc),
            Type = EventType.Flexible
        });

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(403, objectResult.StatusCode);
    }

    [Fact]
    public async Task OverrideBaseOccurrence_SuccessReturnsOkMessage()
    {
        var userId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var service = new FakeEventService
        {
            OverrideResult = (true, string.Empty, eventId)
        };
        var controller = new EventController(service);
        ControllerTestHelper.SetUser(controller, userId);

        var request = new OverrideBaseScheduleOccurrenceRequest
        {
            BaseScheduleEntryId = Guid.NewGuid(),
            OccurrenceDate = new DateOnly(2026, 5, 10),
            Title = "Измененное",
            StartTime = new DateTime(2026, 5, 10, 14, 0, 0, DateTimeKind.Utc),
            EndTime = new DateTime(2026, 5, 10, 15, 0, 0, DateTimeKind.Utc),
            Type = EventType.Flexible
        };

        var result = await controller.OverrideBaseOccurrence(request);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(userId, service.LastOverrideUserId);
        Assert.Equal(eventId, ControllerTestHelper.GetValue<Guid>(ok.Value!, "eventId"));
    }

    [Fact]
    public async Task CancelBaseOccurrence_ServiceErrorMapsToBadRequest()
    {
        var service = new FakeEventService
        {
            CancelResult = (false, "Нельзя скрыть это событие")
        };
        var controller = new EventController(service);
        ControllerTestHelper.SetUser(controller, Guid.NewGuid());

        var result = await controller.CancelBaseOccurrence(new CancelBaseScheduleOccurrenceRequest
        {
            BaseScheduleEntryId = Guid.NewGuid(),
            OccurrenceDate = new DateOnly(2026, 5, 10)
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task DeleteEvent_WhenMissing_ReturnsNotFound()
    {
        var service = new FakeEventService
        {
            DeleteResult = false
        };
        var controller = new EventController(service);
        ControllerTestHelper.SetUser(controller, Guid.NewGuid());

        var result = await controller.DeleteEvent(Guid.NewGuid());

        Assert.IsType<NotFoundObjectResult>(result);
    }

    private sealed class FakeEventService : IEventService
    {
        public List<EventResponse> Schedule { get; set; } = new();
        public (bool IsSuccess, string ErrorMessage, Guid? EventId) CreateResult { get; set; } = (true, string.Empty, Guid.NewGuid());
        public (bool IsSuccess, string ErrorMessage) UpdateResult { get; set; } = (true, string.Empty);
        public (bool IsSuccess, string ErrorMessage, Guid? EventId) OverrideResult { get; set; } = (true, string.Empty, Guid.NewGuid());
        public (bool IsSuccess, string ErrorMessage) CancelResult { get; set; } = (true, string.Empty);
        public bool DeleteResult { get; set; } = true;
        public Guid LastScheduleUserId { get; private set; }
        public Guid LastCreateUserId { get; private set; }
        public CreateEventRequest? LastCreateRequest { get; private set; }
        public Guid LastOverrideUserId { get; private set; }

        public Task<List<EventResponse>> GetUserScheduleAsync(Guid userId)
        {
            LastScheduleUserId = userId;
            return Task.FromResult(Schedule);
        }

        public Task<(bool IsSuccess, string ErrorMessage, Guid? EventId)> CreateEventAsync(Guid userId, CreateEventRequest request)
        {
            LastCreateUserId = userId;
            LastCreateRequest = request;
            return Task.FromResult(CreateResult);
        }

        public Task<(bool IsSuccess, string ErrorMessage)> UpdateEventAsync(Guid userId, Guid eventId, UpdateEventRequest request)
            => Task.FromResult(UpdateResult);

        public Task<(bool IsSuccess, string ErrorMessage, Guid? EventId)> OverrideBaseOccurrenceAsync(Guid userId, OverrideBaseScheduleOccurrenceRequest request)
        {
            LastOverrideUserId = userId;
            return Task.FromResult(OverrideResult);
        }

        public Task<(bool IsSuccess, string ErrorMessage)> CancelBaseOccurrenceAsync(Guid userId, CancelBaseScheduleOccurrenceRequest request)
            => Task.FromResult(CancelResult);

        public Task<bool> DeleteEventAsync(Guid userId, Guid id)
            => Task.FromResult(DeleteResult);
    }
}
