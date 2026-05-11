using FlowMeet.Server.Models.Entities;

namespace FlowMeet.Server.Models.DTOs;

public class ResourceResponse
{
    public int ResourceLevel { get; set; }
    public int RawBalance { get; set; }
    public string StatusMessage { get; set; } = string.Empty;
    public int MoodLevel { get; set; }
    public SleepQuality SleepQuality { get; set; }
    public BackgroundLoadLevel BackgroundLoadLevel { get; set; }
}
