using System.Text.Json.Serialization;

namespace BadgeFed.Models
{
    public class BadgeRecord
    {
        [JsonPropertyName("@context")]
        public string? Context { get; set; } = "https://vocalcat.com/badgefed/1.0";

        public string Type { get; set; } = "BadgeRecord";

        [JsonIgnore]
        public long Id { get; set; }

        [JsonPropertyName("name")]
        public string Title { get; set; } = "";

        public string IssuedBy { get; set; } = "";
        public string Description { get; set; } = "";
        public string Image { get; set; } = "";
        public string ImageAltText { get; set; } = "";

        [System.Text.Json.Serialization.JsonIgnore]
        public string FullImageUrl
        {
            get
            {
                if (string.IsNullOrEmpty(Image))
                {
                    return string.Empty;
                }

                if (Image.StartsWith("http://") || Image.StartsWith("https://") || !IsExternal)
                {
                    return Image;
                }

                // Assuming the image is a relative URL, prepend the base URL
                if (string.IsNullOrEmpty(NoteId))
                {
                    if (string.IsNullOrEmpty(IssuedBy))
                    {
                        return string.Empty;
                    }

                    var issuerUri = new Uri(IssuedBy);
                    return issuerUri.Scheme + "://" + issuerUri.Host + Image;
                }

                var uri = new Uri(NoteId);
                return uri.Scheme + "://" + uri.Host + Image;
            }
        }

        public string EarningCriteria { get; set; } = "";

        public DateTime IssuedOn { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public string IssuedToEmail { get; set; } = "";

        public string IssuedToName { get; set; } = "";

        public string IssuedToSubjectUri { get; set; } = "";

        public DateTime? AcceptedOn { get; set; }
        public DateTime? LastUpdated { get; set; }

        public string Hashtags { get; set; } = "";

        [System.Text.Json.Serialization.JsonIgnore]
        public List<string> HashtagsList { get
            {
                return string.IsNullOrWhiteSpace(Hashtags)
                    ? new List<string>()
                    : Hashtags
                        .Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(h => h.Trim().TrimStart('#'))
                        .Where(h => !string.IsNullOrEmpty(h))
                        .ToList();
            }
        }

        public string FingerPrint { get; set; } = "";

        [JsonPropertyName("id")]
        public string NoteId { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public string AcceptKey { get; set; } = "";

        [System.Text.Json.Serialization.JsonIgnore]
        public Badge? Badge { get; set; } = new Badge();

        [System.Text.Json.Serialization.JsonIgnore]
        public Actor Actor { get; set; } = new Actor();

        [System.Text.Json.Serialization.JsonIgnore]
        public string IssuerFediverseHandle
        {
            get
            {
                return "@" + Actor.Username + "@" + Actor.Domain;
            }
        }

        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsExternal { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public string Visibility { get; set; } = "Public";
    }
}