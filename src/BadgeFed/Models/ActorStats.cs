namespace BadgeFed.Models;

public class ActorStats
{
    public long ActorId { get; set; }

    public int BadgeCount { get; set; } = 0;

    public int FollowerCount { get; set; } = 0;

    public int IssuedCount { get; set; } = 0;

    public int MembersCount { get; set; } = 0;
}