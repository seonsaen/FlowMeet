using FlowMeet.Server.Models.DTOs;
using FlowMeet.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowMeet.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController : AuthorizedApiController
{
    private readonly IDashboardInsightsService _dashboardInsightsService;

    public DashboardController(IDashboardInsightsService dashboardInsightsService)
    {
        _dashboardInsightsService = dashboardInsightsService;
    }

    [HttpGet("me/insights")]
    public async Task<ActionResult<DashboardInsightsResponse>> GetInsights()
    {
        if (!TryGetCurrentUserId(out var userId))
            return UnauthorizedToken<DashboardInsightsResponse>();

        var response = await _dashboardInsightsService.GetInsightsAsync(userId);
        return Ok(response);
    }
}
