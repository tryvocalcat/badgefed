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

    [JsonPropertyName("first")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? First { get; set; }

    [JsonPropertyName("last")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Last { get; set; }

    [JsonPropertyName("orderedItems")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<dynamic>? OrderedItems { get; set; }
}