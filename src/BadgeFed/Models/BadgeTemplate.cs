using System.Text.Json;
using System.Text.Json.Serialization;

namespace BadgeFed.Models
{
    public class BadgeTemplate
    {
        public long Id { get; set; }
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Category { get; set; } = "";
        public string ConfigurationJson { get; set; } = "{}";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [JsonIgnore]
        public BadgeTemplateConfiguration Configuration
        {
            get => string.IsNullOrWhiteSpace(ConfigurationJson)
                ? new BadgeTemplateConfiguration()
                : JsonSerializer.Deserialize<BadgeTemplateConfiguration>(ConfigurationJson) ?? new BadgeTemplateConfiguration();
            set => ConfigurationJson = JsonSerializer.Serialize(value);
        }
    }

    public class BadgeTemplateConfiguration
    {
        [JsonPropertyName("badgeItems")]
        public List<BadgeTemplateItem> BadgeItems { get; set; } = new();

        [JsonPropertyName("variables")]
        public List<BadgeTemplateVariable> Variables { get; set; } = new();
    }

    public class BadgeTemplateItem
    {
        [JsonPropertyName("title")]
        public string Title { get; set; } = "";

        [JsonPropertyName("description")]
        public string Description { get; set; } = "";

        [JsonPropertyName("earningCriteria")]
        public string EarningCriteria { get; set; } = "";

        [JsonPropertyName("badgeType")]
        public string BadgeType { get; set; } = "Badge";

        [JsonPropertyName("hashtags")]
        public string Hashtags { get; set; } = "";

        [JsonPropertyName("isCertificate")]
        public bool IsCertificate { get; set; } = false;

        [JsonPropertyName("imageAltText")]
        public string ImageAltText { get; set; } = "";
    }

    public class BadgeTemplateVariable
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("description")]
        public string Description { get; set; } = "";

        [JsonPropertyName("placeholder")]
        public string Placeholder { get; set; } = "";
    }
}
