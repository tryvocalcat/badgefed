namespace BadgeFed.Models;

public class RecentActivityLog
{
    public string Title { get; set; }

    public string Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? Entity { get; set; }

    public string? EntityId { get; set; }
}
