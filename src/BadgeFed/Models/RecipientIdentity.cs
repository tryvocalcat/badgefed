namespace BadgeFed.Models;

public class RecipientIdentity
{
    public long Id { get; set; }
    public long RecipientId { get; set; }
    public string Provider { get; set; } = "";
    public string ProviderUserId { get; set; } = "";
    public string? ProviderUsername { get; set; }
    public string? ProviderHostname { get; set; }
    public string? ProviderProfileUrl { get; set; }
    public string? ProviderEmail { get; set; }
    public string? ProviderAvatarUrl { get; set; }
    public string? ProviderDisplayName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}
