using FlowMeet.Server.Controllers;
using FlowMeet.Server.Models.DTOs;
using FlowMeet.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace FlowMeet.Server.Tests;

public class ProfileControllerTests
{
    [Fact]
    public async Task GetProfile_WithoutUser_ReturnsUnauthorized()
    {
        var service = new FakeProfileService();
        var controller = new ProfileController(service);
        ControllerTestHelper.SetUser(controller);

        var result = await controller.GetProfile();

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetProfile_ServiceErrorMapsToNotFound()
    {
        var service = new FakeProfileService
        {
            GetProfileResult = (false, "Пользователь не найден", null)
        };
        var controller = new ProfileController(service);
        ControllerTestHelper.SetUser(controller, Guid.NewGuid());

        var result = await controller.GetProfile();

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task UpdateProfile_ReturnsOkAndPassesRequest()
    {
        var userId = Guid.NewGuid();
        var request = new UpdateProfileRequest
        {
            FirstName = "Ваня",
            LastName = "67"
        };
        var expectedProfile = new ProfileResponse
        {
            Id = userId,
            Email = "user@example.com",
            FirstName = "Ваня",
            LastName = "67"
        };
        var service = new FakeProfileService
        {
            UpdateProfileResult = (true, string.Empty, expectedProfile)
        };

        var controller = new ProfileController(service);
        ControllerTestHelper.SetUser(controller, userId);

        var result = await controller.UpdateProfile(request);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var profile = Assert.IsType<ProfileResponse>(ok.Value);

        Assert.Equal(userId, service.LastUpdateProfileUserId);
        Assert.Same(request, service.LastUpdateProfileRequest);
        Assert.Equal("Ваня", profile.FirstName);
    }

    [Fact]
    public async Task RequestEmailChange_ReturnsOkMessage()
    {
        var userId = Guid.NewGuid();
        var service = new FakeProfileService
        {
            RequestEmailChangeResult = (true, string.Empty)
        };
        var controller = new ProfileController(service);
        ControllerTestHelper.SetUser(controller, userId);

        var result = await controller.RequestEmailChange(new EmailChangeRequest
        {
            NewEmail = "new@example.com"
        }, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(userId, service.LastEmailChangeUserId);
        Assert.Equal("new@example.com", service.LastNewEmail);
        Assert.Equal("Код подтверждения отправлен на новую почту", ControllerTestHelper.GetValue<string>(ok.Value!, "message"));
    }

    [Fact]
    public async Task GetBaseScheduleHistory_WithInvalidRange_ReturnsBadRequest()
    {
        var service = new FakeProfileService();
        var controller = new ProfileController(service);
        ControllerTestHelper.SetUser(controller, Guid.NewGuid());

        var result = await controller.GetBaseScheduleHistory(
            new DateOnly(2026, 5, 12),
            new DateOnly(2026, 5, 10));

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task UpdateBaseSchedule_ServiceErrorMapsToBadRequest()
    {
        var service = new FakeProfileService
        {
            UpdateBaseScheduleResult = (false, "Неверный формат времени", null)
        };
        var controller = new ProfileController(service);
        ControllerTestHelper.SetUser(controller, Guid.NewGuid());

        var result = await controller.UpdateBaseSchedule(new List<BaseScheduleEntryDto>());

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    private sealed class FakeProfileService : IProfileService
    {
        public (bool IsSuccess, string ErrorMessage, ProfileResponse? Profile) GetProfileResult { get; set; }
            = (true, string.Empty, new ProfileResponse());
        public (bool IsSuccess, string ErrorMessage, ProfileResponse? Profile) UpdateProfileResult { get; set; }
            = (true, string.Empty, new ProfileResponse());
        public (bool IsSuccess, string ErrorMessage) RequestEmailChangeResult { get; set; } = (true, string.Empty);
        public (bool IsSuccess, string ErrorMessage, ProfileResponse? Profile) ConfirmEmailChangeResult { get; set; }
            = (true, string.Empty, new ProfileResponse());
        public (bool IsSuccess, string ErrorMessage) ChangePasswordResult { get; set; } = (true, string.Empty);
        public List<BaseScheduleEntryDto> BaseSchedule { get; set; } = new();
        public List<BaseScheduleEntryDto> BaseScheduleHistory { get; set; } = new();
        public List<BaseScheduleOccurrenceExceptionDto> Exceptions { get; set; } = new();
        public (bool IsSuccess, string ErrorMessage, List<BaseScheduleEntryDto>? Schedule) UpdateBaseScheduleResult { get; set; }
            = (true, string.Empty, new List<BaseScheduleEntryDto>());

        public Guid LastUpdateProfileUserId { get; private set; }
        public UpdateProfileRequest? LastUpdateProfileRequest { get; private set; }
        public Guid LastEmailChangeUserId { get; private set; }
        public string? LastNewEmail { get; private set; }

        public Task<(bool IsSuccess, string ErrorMessage, ProfileResponse? Profile)> GetProfileAsync(Guid userId)
            => Task.FromResult(GetProfileResult);

        public Task<(bool IsSuccess, string ErrorMessage, ProfileResponse? Profile)> UpdateProfileAsync(Guid userId, UpdateProfileRequest request)
        {
            LastUpdateProfileUserId = userId;
            LastUpdateProfileRequest = request;
            return Task.FromResult(UpdateProfileResult);
        }

        public Task<(bool IsSuccess, string ErrorMessage)> RequestEmailChangeAsync(Guid userId, string newEmail, CancellationToken cancellationToken = default)
        {
            LastEmailChangeUserId = userId;
            LastNewEmail = newEmail;
            return Task.FromResult(RequestEmailChangeResult);
        }

        public Task<(bool IsSuccess, string ErrorMessage, ProfileResponse? Profile)> ConfirmEmailChangeAsync(Guid userId, ConfirmEmailChangeRequest request)
            => Task.FromResult(ConfirmEmailChangeResult);

        public Task<(bool IsSuccess, string ErrorMessage)> ChangePasswordAsync(Guid userId, ChangePasswordRequest request)
            => Task.FromResult(ChangePasswordResult);

        public Task<List<BaseScheduleEntryDto>> GetBaseScheduleAsync(Guid userId)
            => Task.FromResult(BaseSchedule);

        public Task<List<BaseScheduleEntryDto>> GetBaseScheduleHistoryAsync(Guid userId, DateOnly fromDate, DateOnly toDate)
            => Task.FromResult(BaseScheduleHistory);

        public Task<List<BaseScheduleOccurrenceExceptionDto>> GetBaseScheduleExceptionsAsync(Guid userId)
            => Task.FromResult(Exceptions);

        public Task<(bool IsSuccess, string ErrorMessage, List<BaseScheduleEntryDto>? Schedule)> UpdateBaseScheduleAsync(Guid userId, List<BaseScheduleEntryDto> entries)
            => Task.FromResult(UpdateBaseScheduleResult);
    }
}
