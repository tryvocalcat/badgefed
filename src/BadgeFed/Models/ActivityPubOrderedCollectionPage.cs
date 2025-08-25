using System.Text.Json.Serialization;

namespace ActivityPubDotNet.Core;

public class ActivityPubOrderedCollectionPage
{
    [JsonPropertyName("@context")]
    public string Context { get; set; } = "https://www.w3.org/ns/activitystreams";

    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = "OrderedCollectionPage";

    [JsonPropertyName("partOf")]
    public string PartOf { get; set; }

    [JsonPropertyName("next")]
    public string? Next { get; set; }

    [JsonPropertyName("prev")]
    public string? Prev { get; set; }

    [JsonPropertyName("orderedItems")]
    public List<object> OrderedItems { get; set; } = [];
}
