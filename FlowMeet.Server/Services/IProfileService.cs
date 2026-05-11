using FlowMeet.Server.Models.DTOs;

namespace FlowMeet.Server.Services;

public interface IProfileService
{
    Task<(bool IsSuccess, string ErrorMessage, ProfileResponse? Profile)> GetProfileAsync(Guid userId);
    Task<(bool IsSuccess, string ErrorMessage, ProfileResponse? Profile)> UpdateProfileAsync(Guid userId, UpdateProfileRequest request);
    Task<(bool IsSuccess, string ErrorMessage)> RequestEmailChangeAsync(Guid userId, string newEmail, CancellationToken cancellationToken = default);
    Task<(bool IsSuccess, string ErrorMessage, ProfileResponse? Profile)> ConfirmEmailChangeAsync(Guid userId, ConfirmEmailChangeRequest request);
    Task<(bool IsSuccess, string ErrorMessage)> ChangePasswordAsync(Guid userId, ChangePasswordRequest request);
    Task<List<BaseScheduleEntryDto>> GetBaseScheduleAsync(Guid userId);
    Task<List<BaseScheduleEntryDto>> GetBaseScheduleHistoryAsync(Guid userId, DateOnly fromDate, DateOnly toDate);
    Task<List<BaseScheduleOccurrenceExceptionDto>> GetBaseScheduleExceptionsAsync(Guid userId);
    Task<(bool IsSuccess, string ErrorMessage, List<BaseScheduleEntryDto>? Schedule)> UpdateBaseScheduleAsync(Guid userId, List<BaseScheduleEntryDto> entries);
}
