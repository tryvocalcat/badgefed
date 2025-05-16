
namespace BadgeFed.Services;

public class FollowedIssuer
{
    public long Id { get; set; }
    public string Name { get; set; }
    public string Url { get; set; }
    public string Inbox { get; set; }
    public string Outbox { get; set; }
    public long ActorId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public int? TotalIssued { get; set; }

    public string? AvatarUri { get; set; }
}