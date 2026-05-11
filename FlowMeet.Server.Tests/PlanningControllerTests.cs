using FlowMeet.Server.Controllers;
using FlowMeet.Server.Models.DTOs;
using FlowMeet.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace FlowMeet.Server.Tests;

public class PlanningControllerTests
{
    [Fact]
    public async Task FindSlots_WithoutUser_ReturnsUnauthorized()
    {
        var service = new FakePlanningService();
        var controller = new PlanningController(service);
        ControllerTestHelper.SetUser(controller);

        var result = await controller.FindSlots(new PlanningRequest
        {
            ParticipantIds = new List<Guid>(),
            StartDate = new DateOnly(2026, 5, 10),
            DurationMinutes = 60
        });

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    [Fact]
    public async Task FindSlots_WithMissingParticipants_ReturnsBadRequest()
    {
        var service = new FakePlanningService();
        var controller = new PlanningController(service);
        ControllerTestHelper.SetUser(controller, Guid.NewGuid());

        var result = await controller.FindSlots(new PlanningRequest
        {
            ParticipantIds = null!,
            StartDate = new DateOnly(2026, 5, 10),
            DurationMinutes = 60
        });

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task FindSlots_AddsCurrentUserAndReturnsOkSlots()
    {
        var currentUserId = Guid.NewGuid();
        var friendId = Guid.NewGuid();
        var service = new FakePlanningService
        {
            Slots = new List<TimeSlotDto>
            {
                new()
                {
                    StartTime = new DateTime(2026, 5, 10, 11, 0, 0, DateTimeKind.Utc),
                    EndTime = new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc),
                    Suitability = "Optimal",
                    Description = "Свободное время"
                }
            }
        };

        var controller = new PlanningController(service);
        ControllerTestHelper.SetUser(controller, currentUserId);

        var result = await controller.FindSlots(new PlanningRequest
        {
            ParticipantIds = new List<Guid> { friendId, currentUserId, friendId },
            StartDate = new DateOnly(2026, 5, 10),
            DurationMinutes = 60
        });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var slots = Assert.IsType<List<TimeSlotDto>>(ok.Value);

        Assert.Equal(currentUserId, service.ReceivedUserId);
        Assert.Equal(new[] { friendId, currentUserId }, service.ReceivedParticipantIds!.ToArray());
        Assert.Single(slots);
        Assert.Equal("Optimal", slots[0].Suitability);
    }

    [Fact]
    public async Task FindSlots_ServiceErrorMapsToNotFound()
    {
        var service = new FakePlanningService
        {
            IsSuccess = false,
            ErrorMessage = "Один или несколько участников не найдены"
        };
        var controller = new PlanningController(service);
        ControllerTestHelper.SetUser(controller, Guid.NewGuid());

        var result = await controller.FindSlots(new PlanningRequest
        {
            ParticipantIds = new List<Guid>(),
            StartDate = new DateOnly(2026, 5, 10),
            DurationMinutes = 60
        });

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    private sealed class FakePlanningService : IPlanningService
    {
        public bool IsSuccess { get; set; } = true;
        public string ErrorMessage { get; set; } = string.Empty;
        public List<TimeSlotDto> Slots { get; set; } = new();
        public Guid ReceivedUserId { get; private set; }
        public List<Guid>? ReceivedParticipantIds { get; private set; }

        public Task<(bool IsSuccess, string ErrorMessage, List<TimeSlotDto> Slots)> FindGroupSlotsAsync(Guid currentUserId, List<Guid> participantIds, DateOnly startDate, int durationMinutes)
        {
            ReceivedUserId = currentUserId;
            ReceivedParticipantIds = participantIds;
            return Task.FromResult((IsSuccess, ErrorMessage, Slots));
        }
    }
}
