namespace BadgeFed.Models;

public class FederationEvent
{
    public long Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string? ActorUri { get; set; }
    public string? ObjectUri { get; set; }
    public string? TargetUri { get; set; }
    public string? RemoteHost { get; set; }
    public string? RequestIp { get; set; }
    public string? UserAgent { get; set; }
    public string? GroupId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public static class FederationEventType
{
    // Outbound events
    public const string BadgeBroadcast = "badge_broadcast";
    public const string BadgeAnnounce = "badge_announce";
    public const string BadgeDeliveredToFollower = "badge_delivered_to_follower";
    public const string BadgeRevocationSent = "badge_revocation_sent";
    public const string FollowAccepted = "follow_accepted";

    // Inbound events
    public const string InboxFollow = "inbox_follow";
    public const string InboxUndoFollow = "inbox_undo_follow";
    public const string InboxCreate = "inbox_create";
    public const string InboxAnnounce = "inbox_announce";

    // Request events
    public const string ActorRequested = "actor_requested";
    public const string BadgeRequested = "badge_requested";
    public const string OutboxRequested = "outbox_requested";
    public const string WebFingerRequested = "webfinger_requested";
    public const string OpenBadgeIssuerRequested = "openbadge_issuer_requested";
    public const string OpenBadgeClassRequested = "openbadge_class_requested";
    public const string OpenBadgeAssertionRequested = "openbadge_assertion_requested";
    public const string BadgeClassRequested = "badge_class_requested";
}
