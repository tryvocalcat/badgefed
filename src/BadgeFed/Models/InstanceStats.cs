namespace BadgeFed.Models;

public class InstanceStats
{
    public int FollowerCount { get; set; } = 0;

    public int IssuedCount { get; set; } = 0;

    public int AcceptedCount { get; set; } = 0;

    public int PendingCount { get; set; } = 0;

    public int FollowedInstancesCount { get; set; } = 0;

    public int ExternalBadgesCount { get; set; } = 0;
}