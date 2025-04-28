using System.Text.Json.Serialization;

namespace ActivityPubDotNet.Core;

public class FollowActivity
{
    [JsonPropertyName("@context")]
    public string? _context { get; set; } = "https://www.w3.org/ns/activitystreams";
    public string? Id { get; set; }
    public string? Type { get; set; } = "Follow";
    public string? Actor { get; set; }

     [JsonPropertyName("object")]
    public string? _object { get; set; }
}