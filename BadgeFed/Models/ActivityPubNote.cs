using System.Text.Json.Serialization;
using BadgeFed.Models;

namespace ActivityPubDotNet.Core;

public class ActivityPubNote
{
    [JsonPropertyName("@context")]
    public string Context { get; set; } = "https://www.w3.org/ns/activitystreams";

    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = "Note";

    [JsonPropertyName("content")]
    public string Content { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; }

    [JsonPropertyName("attributedTo")]
    public string AttributedTo { get; set; }

    public BadgeRecord BadgeMetadata { get; set; }

    [JsonPropertyName("to")]
    public List<string> To { get; set; } = new List<string> { "https://www.w3.org/ns/activitystreams#Public" };

    [JsonPropertyName("cc")]
    public List<string> Cc { get; set; } = new List<string>();

    [JsonPropertyName("published")]
    public DateTime Published { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("tags")]
    public List<Tag> Tags { get; set; } = [];

    [JsonPropertyName("replies")]
    public Collection Replies { get; set; } = new Collection();

    public class Tag
    {
        public string Type { get; set; }
        public string Href { get; set; }
        public string Name { get; set; }
    }

    public class Collection
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; } = "Collection";

        [JsonPropertyName("first")]
        public CollectionPage First { get; set; } = new CollectionPage();
    }

    public class CollectionPage
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "CollectionPage";

        [JsonPropertyName("next")]
        public string Next { get; set; }

        [JsonPropertyName("partOf")]
        public string PartOf { get; set; }

        [JsonPropertyName("items")]
        public List<string> Items { get; set; } = new List<string>();
    }
}

