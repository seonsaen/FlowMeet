using FlowMeet.Server.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace FlowMeet.Server.Tests;

public class AuthorizedApiControllerTests
{
    [Fact]
    public void ErrorResult_ReturnsNotFoundForMissingEntity()
    {
        var controller = new TestController();

        var result = controller.InvokeErrorResult("Группа не найдена");

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public void ErrorResult_ReturnsForbiddenForPermissionErrors()
    {
        var controller = new TestController();

        var result = controller.InvokeErrorResult("У вас нет прав удалить этого участника");

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, objectResult.StatusCode);
    }

    [Fact]
    public void ErrorResult_ReturnsBadRequestForGenericErrors()
    {
        var controller = new TestController();

        var result = controller.InvokeErrorResult("Время уведомления должно быть в будущем");

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void UnauthorizedToken_ReturnsUnauthorized()
    {
        var controller = new TestController();

        var result = controller.InvokeUnauthorizedToken();

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    private sealed class TestController : AuthorizedApiController
    {
        public ActionResult InvokeErrorResult(string message) => ErrorResult(message);

        public ActionResult InvokeUnauthorizedToken() => UnauthorizedToken();
    }
}
