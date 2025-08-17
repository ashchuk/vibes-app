namespace Vibes.API.Models;

public class VibesMetric
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public bool PositiveVibe { get; set; }
    public DateTime RecordDate { get; set; }
}