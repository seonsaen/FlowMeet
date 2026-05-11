using FlowMeet.Server.Models.DTOs;

namespace FlowMeet.Server.Services;

public interface IMeetingService
{
    Task<(bool IsSuccess, string ErrorMessage)> CreateMeetingAsync(Guid userId, CreateMeetingRequest request);
    Task<(bool IsSuccess, string ErrorMessage)> UpdateMeetingAsync(Guid userId, Guid meetingId, UpdateMeetingRequest request);
    Task<(bool IsSuccess, string ErrorMessage)> DeleteMeetingAsync(Guid userId, Guid meetingId);
    Task<List<IncomingInviteDto>> GetIncomingInvitesAsync(Guid userId);
    Task<List<OutgoingInviteDto>> GetOutgoingInvitesAsync(Guid userId);
    Task<List<MeetingOverviewDto>> GetMyMeetingsAsync(Guid userId);
    Task<List<MeetingOverviewDto>> GetMeetingHistoryAsync(Guid userId);
    Task<(bool IsSuccess, string ErrorMessage)> RespondToInviteAsync(Guid userId, RespondToInviteRequest request);
}
