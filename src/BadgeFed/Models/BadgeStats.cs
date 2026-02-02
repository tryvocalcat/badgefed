namespace BadgeFed.Models;

public class BadgeStats
{
    public long BadgeId { get; set; }

    public int IssuedCount { get; set; } = 0;

    public int AcceptedCount { get; set; } = 0;

    public int RevokedCount { get; set; } = 0;
}