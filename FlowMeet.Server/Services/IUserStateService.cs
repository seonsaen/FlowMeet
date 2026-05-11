using FlowMeet.Server.Models.DTOs;

namespace FlowMeet.Server.Services;

public interface IUserStateService
{
    Task<(int ResourceLevel, string Message)> SetMoodAsync(Guid userId, MoodRequest request);
    Task<ResourceResponse> GetResourceStatusAsync(Guid userId);
    Task<int> GetProjectedResourceAsync(Guid userId, DateTime momentUtc);
    Task<Dictionary<DateTime, int>> GetProjectedResourcesAsync(Guid userId, IReadOnlyCollection<DateTime> momentsUtc);
}
