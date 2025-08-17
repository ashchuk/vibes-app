namespace Vibes.API.Models;

public class ExtractedEvent
{
    public string Title { get; set; } = string.Empty;
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public bool Found { get; set; } = false;
}