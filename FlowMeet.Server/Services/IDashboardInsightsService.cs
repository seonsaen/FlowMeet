using FlowMeet.Server.Models.DTOs;

namespace FlowMeet.Server.Services;

public interface IDashboardInsightsService
{
    Task<DashboardInsightsResponse> GetInsightsAsync(Guid userId);
}
