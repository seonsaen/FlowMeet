using FlowMeet.Server.Controllers;
using FlowMeet.Server.Models.DTOs;
using FlowMeet.Server.Models.Entities;
using FlowMeet.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace FlowMeet.Server.Tests;

public class UserStateControllerTests
{
    [Fact]
    public async Task SetMood_WithoutUser_ReturnsUnauthorized()
    {
        var service = new FakeUserStateService();
        var controller = new UserStateController(service);
        ControllerTestHelper.SetUser(controller);

        var result = await controller.SetMood(new MoodRequest { MoodLevel = 4 });

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task SetMood_WithUser_ReturnsPayloadAndPassesRequest()
    {
        var userId = Guid.NewGuid();
        var service = new FakeUserStateService
        {
            SetMoodResult = (67, "Состояние дня сохранено")
        };
        var controller = new UserStateController(service);
        ControllerTestHelper.SetUser(controller, userId);

        var request = new MoodRequest
        {
            MoodLevel = 5,
            SleepQuality = SleepQuality.Good,
            BackgroundLoadLevel = BackgroundLoadLevel.Tense
        };

        var result = await controller.SetMood(request);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(userId, service.LastMoodUserId);
        Assert.Same(request, service.LastMoodRequest);
        Assert.Equal("Состояние дня сохранено", ControllerTestHelper.GetValue<string>(ok.Value!, "Message"));
        Assert.Equal(67, ControllerTestHelper.GetValue<int>(ok.Value!, "CurrentResource"));
    }

    [Fact]
    public async Task GetResource_ReturnsOkResponse()
    {
        var userId = Guid.NewGuid();
        var service = new FakeUserStateService
        {
            ResourceResponse = new ResourceResponse
            {
                ResourceLevel = 42,
                RawBalance = 40,
                MoodLevel = 3,
                SleepQuality = SleepQuality.Normal,
                BackgroundLoadLevel = BackgroundLoadLevel.Heavy,
                StatusMessage = "Ресурс снижен"
            }
        };
        var controller = new UserStateController(service);
        ControllerTestHelper.SetUser(controller, userId);

        var result = await controller.GetResource();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ResourceResponse>(ok.Value);

        Assert.Equal(userId, service.LastResourceUserId);
        Assert.Equal(42, response.ResourceLevel);
        Assert.Equal("Ресурс снижен", response.StatusMessage);
    }

    private sealed class FakeUserStateService : IUserStateService
    {
        public (int ResourceLevel, string Message) SetMoodResult { get; set; } = (50, "ok");
        public ResourceResponse ResourceResponse { get; set; } = new();
        public Guid LastMoodUserId { get; private set; }
        public MoodRequest? LastMoodRequest { get; private set; }
        public Guid LastResourceUserId { get; private set; }

        public Task<(int ResourceLevel, string Message)> SetMoodAsync(Guid userId, MoodRequest request)
        {
            LastMoodUserId = userId;
            LastMoodRequest = request;
            return Task.FromResult(SetMoodResult);
        }

        public Task<ResourceResponse> GetResourceStatusAsync(Guid userId)
        {
            LastResourceUserId = userId;
            return Task.FromResult(ResourceResponse);
        }

        public Task<int> GetProjectedResourceAsync(Guid userId, DateTime momentUtc)
            => Task.FromResult(0);

        public Task<Dictionary<DateTime, int>> GetProjectedResourcesAsync(Guid userId, IReadOnlyCollection<DateTime> momentsUtc)
            => Task.FromResult(new Dictionary<DateTime, int>());
    }
}
