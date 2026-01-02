using System.Text.Json.Serialization;
using BadgeFed.Models;

namespace ActivityPubDotNet.Core;

public class ActivityPubNote
{
    [JsonPropertyName("@context")]
    public object Context { get; set; } = new object[]
    {
        "https://www.w3.org/ns/activitystreams",
        new
        {
            gts = "https://gotosocial.org/ns#",
            interactionPolicy = new Dictionary<string, object>
            {
               { "@id", "gts:interactionPolicy" },
               { "@type", "@id" }
            },
            canQuote = new Dictionary<string, object>
            {
               { "@id", "gts:canQuote" },
               { "@type", "@id" }
            },
            automaticApproval = new Dictionary<string, object>
            {
                { "@id", "gts:automaticApproval" },
                { "@type", "@id" }
            }
        }
    };

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

    [JsonPropertyName("attachment")]
    public List<object> Attachment { get; set; } = new List<object>();

    [JsonPropertyName("vocalcat:badgeAssertion")]
    public BadgeRecord? BadgeMetadata { get; set; }

    [JsonPropertyName("openbadges:assertion")]
    public object OpenBadge { get; set; }

    [JsonPropertyName("to")]
    public List<string> To { get; set; } = new List<string> { "https://www.w3.org/ns/activitystreams#Public" };

    [JsonPropertyName("cc")]
    public List<string> Cc { get; set; } = new List<string>();

    [JsonPropertyName("published")]
    public DateTime Published { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("tag")]
    public List<Tag> Tags { get; set; } = [];

    [JsonPropertyName("replies")]
    public Collection Replies { get; set; } = new Collection();

    [JsonPropertyName("inReplyTo")]
    public string InReplyTo { get; set; } = default!;

    [JsonPropertyName("interactionPolicy")]
    public InteractionPolicyDefinition InteractionPolicy { get; set; } = new InteractionPolicyDefinition();

    public class InteractionPolicyDefinition
    {
        [JsonPropertyName("canQuote")]
        public CanQuoteDefinition CanQuote { get; set; } = new CanQuoteDefinition();
    }

    public class CanQuoteDefinition
    {
        [JsonPropertyName("automaticApproval")]
        public object AutomaticApproval { get; set; } = "https://www.w3.org/ns/activitystreams#Public";
    }

    public class Tag
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }
        [JsonPropertyName("href")]
        public string Href { get; set; }
        [JsonPropertyName("name")]
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

