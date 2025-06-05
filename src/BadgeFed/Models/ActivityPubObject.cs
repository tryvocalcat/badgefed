using System.Text.Json.Serialization;
using BadgeFed.Models;

namespace ActivityPubDotNet.Core;

public class ActivityPubObject
{
    [JsonPropertyName("@context")]
    public string Context { get; set; } = "https://www.w3.org/ns/activitystreams";

    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("object")]
    public string Object { get; set; }
}

