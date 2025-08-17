namespace Vibes.API.Models;

public class DailyPlan
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public VibesUser User { get; set; } = null!;
    public DateOnly PlanDate { get; set; }
    public string PlanText { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
}