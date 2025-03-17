using System.Text.Json.Serialization;

namespace ActivityPubDotNet.Core;

public class ActivityPubCollection
{
    [JsonPropertyName("@context")]
    public string Context { get; set; } = "https://www.w3.org/ns/activitystreams";

    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = "OrderedCollection";

    [JsonPropertyName("totalItems")]
    public int TotalItems { get; set; }

    [JsonPropertyName("orderedItems")]
    public List<dynamic> OrderedItems { get; set; } = [];
}