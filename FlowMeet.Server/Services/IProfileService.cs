using FlowMeet.Server.Models.DTOs;

namespace FlowMeet.Server.Services;

public interface IProfileService
{
    Task<(bool IsSuccess, string ErrorMessage, ProfileResponse? Profile)> GetProfileAsync(Guid userId);
    Task<(bool IsSuccess, string ErrorMessage, ProfileResponse? Profile)> UpdateProfileAsync(Guid userId, UpdateProfileRequest request);
    Task<List<BaseScheduleEntryDto>> GetBaseScheduleAsync(Guid userId);
    Task<(bool IsSuccess, string ErrorMessage, List<BaseScheduleEntryDto>? Schedule)> UpdateBaseScheduleAsync(Guid userId, List<BaseScheduleEntryDto> entries);
}