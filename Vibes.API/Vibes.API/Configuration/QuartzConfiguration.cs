namespace Vibes.API.Configuration;

public class QuartzConfiguration
{
    public string MorningCheckupCronSchedule { get; init; } = default!;
    public string EveningCheckupCronSchedule { get; init; } = default!;
}