namespace Vibes.API.Models;

public enum VibeType
{
    Energize,
    Neutral,
    Drain
}

public class EventRating
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public VibesUser User { get; set; } = null!;
    
    // ID события из Google Calendar. Он уникален в рамках одного календаря.
    public string GoogleEventId { get; set; } = string.Empty;
    public string EventSummary { get; set; } = string.Empty; // Название события для удобства
    
    public VibeType Vibe { get; set; }
    
    public DateTime RatedAtUtc { get; set; }
}