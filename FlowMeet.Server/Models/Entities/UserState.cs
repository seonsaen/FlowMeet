using System.ComponentModel.DataAnnotations;

namespace FlowMeet.Server.Models.Entities;

public enum SleepQuality
{
    Poor = 0,
    Normal = 1,
    Good = 2
}

public enum BackgroundLoadLevel
{
    Calm = 0,
    Tense = 1,
    Heavy = 2
}

public class UserState
{
    [Key]
    public int Id { get; set; }

    [Required]
    public Guid UserId { get; set; }
    public User? User { get; set; }

    [Required]
    public DateOnly Date { get; set; }

    [Range(1, 5)]
    public int MoodLevel { get; set; }

    [Range(0, 100)]
    public int ResourceLevel { get; set; }

    [Range(-200, 100)]
    public int RawBalance { get; set; }

    public SleepQuality SleepQuality { get; set; } = SleepQuality.Normal;

    public BackgroundLoadLevel BackgroundLoadLevel { get; set; } = BackgroundLoadLevel.Calm;

    public string? Note { get; set; }
}