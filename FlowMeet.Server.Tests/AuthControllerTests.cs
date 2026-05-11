using FlowMeet.Server.Controllers;
using FlowMeet.Server.Models.DTOs;
using FlowMeet.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace FlowMeet.Server.Tests;

public class AuthControllerTests
{
    [Fact]
    public async Task Register_ServiceFailureReturnsBadRequest()
    {
        var service = new FakeAuthService
        {
            RegisterResult = (false, "Пользователь с таким email уже существует")
        };
        var controller = new AuthController(service);

        var result = await controller.Register(new RegisterRequest
        {
            Email = "user@example.com",
            Password = "password",
            FirstName = "Test",
            LastName = "User"
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Register_SuccessReturnsOkMessageAndPassesRequest()
    {
        var service = new FakeAuthService
        {
            RegisterResult = (true, string.Empty)
        };
        var controller = new AuthController(service);
        var request = new RegisterRequest
        {
            Email = "user@example.com",
            Password = "password",
            FirstName = "Test",
            LastName = "User"
        };

        var result = await controller.Register(request);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(request, service.LastRegisterRequest);
        Assert.Equal("Код подтверждения отправлен на почту", ControllerTestHelper.GetValue<string>(ok.Value!, "message"));
    }

    [Fact]
    public async Task Login_InvalidCredentialsReturnsUnauthorized()
    {
        var service = new FakeAuthService
        {
            LoginResult = null
        };
        var controller = new AuthController(service);

        var result = await controller.Login(new LoginRequest
        {
            Email = "user@example.com",
            Password = "wrong"
        });

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task RequestPasswordReset_ReturnsOkResponse()
    {
        var expected = new PasswordResetRequestResponse
        {
            Message = "Если аккаунт существует, инструкция отправлена"
        };
        var service = new FakeAuthService
        {
            PasswordResetResponse = expected
        };
        var controller = new AuthController(service);

        var result = await controller.RequestPasswordReset(new PasswordResetRequest
        {
            Email = "user@example.com"
        });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<PasswordResetRequestResponse>(ok.Value);
        Assert.Equal(expected.Message, response.Message);
    }

    [Fact]
    public async Task ConfirmPasswordReset_SuccessReturnsOkMessage()
    {
        var service = new FakeAuthService
        {
            ConfirmPasswordResetResult = (true, string.Empty)
        };
        var controller = new AuthController(service);
        var request = new PasswordResetConfirmRequest
        {
            Email = "user@example.com",
            Code = "123456",
            NewPassword = "new-password"
        };

        var result = await controller.ConfirmPasswordReset(request);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(request, service.LastPasswordResetConfirmRequest);
        Assert.Equal("Пароль успешно обновлен", ControllerTestHelper.GetValue<string>(ok.Value!, "message"));
    }

    private sealed class FakeAuthService : IAuthService
    {
        public (bool IsSuccess, string ErrorMessage) RegisterResult { get; set; } = (true, string.Empty);
        public (bool IsSuccess, string ErrorMessage) ConfirmRegistrationResult { get; set; } = (true, string.Empty);
        public AuthResponse? LoginResult { get; set; } = new();
        public PasswordResetRequestResponse PasswordResetResponse { get; set; } = new();
        public (bool IsSuccess, string ErrorMessage) ConfirmPasswordResetResult { get; set; } = (true, string.Empty);
        public RegisterRequest? LastRegisterRequest { get; private set; }
        public PasswordResetConfirmRequest? LastPasswordResetConfirmRequest { get; private set; }

        public Task<(bool IsSuccess, string ErrorMessage)> RegisterAsync(RegisterRequest request)
        {
            LastRegisterRequest = request;
            return Task.FromResult(RegisterResult);
        }

        public Task<(bool IsSuccess, string ErrorMessage)> ConfirmRegistrationAsync(ConfirmRegistrationRequest request)
            => Task.FromResult(ConfirmRegistrationResult);

        public Task<AuthResponse?> LoginAsync(LoginRequest request)
            => Task.FromResult(LoginResult);

        public Task<PasswordResetRequestResponse> RequestPasswordResetAsync(PasswordResetRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(PasswordResetResponse);

        public Task<(bool IsSuccess, string ErrorMessage)> ConfirmPasswordResetAsync(PasswordResetConfirmRequest request)
        {
            LastPasswordResetConfirmRequest = request;
            return Task.FromResult(ConfirmPasswordResetResult);
        }
    }
}
