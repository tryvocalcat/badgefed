using System.Text.Json.Serialization;

namespace ActivityPubDotNet.Core;

public class ActivityPubCreate
{
    [JsonPropertyName("@context")]
    public string Context { get; set; } = "https://www.w3.org/ns/activitystreams";

    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = "Create";

    [JsonPropertyName("actor")]
    public string Actor { get; set; }

    [JsonPropertyName("to")]
    public List<string> To { get; set; } = new List<string> { "https://www.w3.org/ns/activitystreams#Public" };

    [JsonPropertyName("cc")]
    public List<string> Cc { get; set; } = new List<string>();

    [JsonPropertyName("published")]
    public DateTime Published { get; set; }

    [JsonPropertyName("object")]
    public object Object { get; set; }

    [JsonPropertyName("signature")]
    public dynamic Signature { get; set; }
}