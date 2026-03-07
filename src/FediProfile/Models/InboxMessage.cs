using System.Text.Json;
using System.Text.Json.Serialization;

namespace FediProfile.Models;

// Incoming ActivityPub message from Inbox
public class InboxMessage
{
    [JsonPropertyName("@context")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Context { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("actor")]
    public string? Actor { get; set; }

    [JsonPropertyName("object")]
    public object? Object { get; set; }

    [JsonPropertyName("target")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Target { get; set; }

    public bool IsFollow() => Type?.Equals("Follow", StringComparison.OrdinalIgnoreCase) == true;

    public bool IsUndo() => Type?.Equals("Undo", StringComparison.OrdinalIgnoreCase) == true;

    public bool IsCreate() => Type?.Equals("Create", StringComparison.OrdinalIgnoreCase) == true;

    public bool IsAnnounce() => Type?.Equals("Announce", StringComparison.OrdinalIgnoreCase) == true;

    public string? GetFollowActor()
    {
        if (IsFollow() && !string.IsNullOrEmpty(Actor))
            return Actor;

        if (IsUndo() && Object is JsonElement elem)
        {
            if (elem.ValueKind == System.Text.Json.JsonValueKind.Object &&
                elem.TryGetProperty("type", out var typeElem) &&
                typeElem.GetString()?.Equals("Follow", StringComparison.OrdinalIgnoreCase) == true &&
                elem.TryGetProperty("actor", out var actorElem))
            {
                return actorElem.GetString();
            }
        }

        return null;
    }

    /// <summary>
    /// For Undo/Follow activities, extracts the "object" of the inner Follow
    /// (i.e. the actor being followed / unfollowed).
    /// </summary>
    public string? GetFollowObject()
    {
        if (IsUndo() && Object is JsonElement elem)
        {
            if (elem.ValueKind == System.Text.Json.JsonValueKind.Object &&
                elem.TryGetProperty("type", out var typeElem) &&
                typeElem.GetString()?.Equals("Follow", StringComparison.OrdinalIgnoreCase) == true &&
                elem.TryGetProperty("object", out var objElem))
            {
                return objElem.GetString();
            }
        }

        return null;
    }
}

// Accept response
public class AcceptActivity
{
    [JsonPropertyName("@context")]
    public string Context { get; set; } = "https://www.w3.org/ns/activitystreams";

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "Accept";

    [JsonPropertyName("actor")]
    public string Actor { get; set; } = string.Empty;

    [JsonPropertyName("object")]
    public object Object { get; set; } = new();

    [JsonPropertyName("to")]
    public string[] To { get; set; } = Array.Empty<string>();
}

// Announce activity
public class AnnounceActivity
{
    [JsonPropertyName("@context")]
    public string Context { get; set; } = "https://www.w3.org/ns/activitystreams";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "Announce";

    [JsonPropertyName("actor")]
    public string Actor { get; set; } = string.Empty;

    [JsonPropertyName("object")]
    public string Object { get; set; } = string.Empty;

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("to")]
    public string[] To { get; set; } = Array.Empty<string>();

    [JsonPropertyName("cc")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? Cc { get; set; }
}
