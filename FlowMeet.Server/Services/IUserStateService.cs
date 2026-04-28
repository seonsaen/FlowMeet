using FlowMeet.Server.Models.DTOs;

namespace FlowMeet.Server.Services;

public interface IUserStateService
{
    Task<(int ResourceLevel, string Message)> SetMoodAsync(Guid userId, MoodRequest request);
    Task<ResourceResponse> GetResourceStatusAsync(Guid userId);
}