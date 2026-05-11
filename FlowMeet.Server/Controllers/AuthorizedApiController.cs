using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace FlowMeet.Server.Controllers;

public abstract class AuthorizedApiController : ControllerBase
{
    protected bool TryGetCurrentUserId(out Guid userId)
    {
        userId = Guid.Empty;

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return !string.IsNullOrWhiteSpace(userIdClaim) && Guid.TryParse(userIdClaim, out userId);
    }

    protected ActionResult UnauthorizedToken() => Unauthorized(new { error = "Некорректный токен" });

    protected ActionResult<T> UnauthorizedToken<T>() => new(UnauthorizedToken());

    protected ActionResult ErrorResult(string? message)
    {
        var errorMessage = string.IsNullOrWhiteSpace(message) ? "Операция не выполнена" : message;
        var normalized = errorMessage.ToLowerInvariant();

        if (normalized.Contains("некорректный токен"))
            return Unauthorized(new { error = errorMessage });

        if (normalized.Contains("не найден") || normalized.Contains("не найдена") || normalized.Contains("не найдены"))
            return NotFound(new { error = errorMessage });

        if (normalized.Contains("нет прав")
            || normalized.Contains("может только")
            || normalized.Contains("не состоите")
            || normalized.Contains("нельзя изменить роль владельца")
            || normalized.Contains("нельзя удалить владельца")
            || normalized.Contains("нельзя покинуть группу владельцу"))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = errorMessage });
        }

        return BadRequest(new { error = errorMessage });
    }

    protected ActionResult<T> ErrorResult<T>(string? message) => new(ErrorResult(message));
}
