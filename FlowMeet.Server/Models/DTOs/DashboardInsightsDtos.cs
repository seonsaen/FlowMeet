namespace FlowMeet.Server.Models.DTOs;

public class DashboardInsightsResponse
{
    public int MeetingsLast30Days { get; set; }
    public int TotalPastMeetings { get; set; }
    public int ConfirmedFutureMeetings { get; set; }
    public double? AverageMoodLast14Days { get; set; }
    public double? AverageResourceLast14Days { get; set; }
    public string? OverloadWarning { get; set; }
    public List<FrequentParticipantDto> FrequentParticipants { get; set; } = new();
    public List<DashboardRecommendationDto> Recommendations { get; set; } = new();
}

public class FrequentParticipantDto
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int MeetingsCount { get; set; }
    public DateTime? LastMeetingAt { get; set; }
}

public class DashboardRecommendationDto
{
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Guid? UserId { get; set; }
    public string? UserName { get; set; }
    public DateTime? SuggestedStartTime { get; set; }
    public DateTime? SuggestedEndTime { get; set; }
}
