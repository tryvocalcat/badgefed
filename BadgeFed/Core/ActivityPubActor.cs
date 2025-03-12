using System.Text.Json.Serialization;

namespace ActivityPubDotNet.Core
{
    public class ActivityPubActor
    {
        public class PublicKeyDefinition
        {
            public string Id { get; set; } = default!;
            public string Owner { get; set; } = default!;
            public string PublicKeyPem { get; set; } = default!;
        }

        public class EndpointsDefinition
        {
            public string? SharedInbox { get; set; } = default!;
        }

        [JsonPropertyName("@context")]
        public object Context { get; set; } = default!;

        public string Id { get; set; } = default!;
        public string Type { get; set; } = default!;
        
        public string Outbox { get; set; } = default!;
        public string Following { get; set; } = default!;
        public string Followers { get; set; } = default!;
        public string Inbox { get; set; } = default!;
        public string PreferredUsername { get; set; } = default!;
        public string Name { get; set; } = default!;
        public string Summary { get; set; } = default!;
        public string Url { get; set; } = default!;
        public PublicKeyDefinition PublicKey { get; set; } = default!;

        public EndpointsDefinition Endpoints { get; set; } = default!;
        public bool Discoverable { get; internal set; }
        public bool Memorial { get; internal set; }

        public ActivityPubImage Icon { get; set; }

        public ActivityPubImage Image { get; set; }

        public List<Attachment> Attachment { get; set; } = new List<Attachment>();
    }

     public class Attachment
        {
            public string Type { get; set; } = "PropertyValue";
            public string Name { get; set; } = default!;
            public string Value { get; set; } = default!;
        }

    public class ActivityPubImage
    {
        public string Type { get; set; } = default!;
        public string MediaType { get; set; } = default!;
        public string Url { get; set; } = default!;
    }
}