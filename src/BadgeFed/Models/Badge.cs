namespace BadgeFed.Models
{
    public class Badge
    {
        public long Id { get; set; }

        public string Title { get; set; } = "";

        public string Description { get; set; } = "";

        public long IssuedBy { get; set; }

        public Actor? Issuer { get; set; }

        public string Image { get; set; } = "";
        public string ImageAltText { get; set; } = "";

        public string EarningCriteria { get; set; } = "";

        public string BadgeType { get; set; } = "Badge";

        public static List<string> BadgeTypes = new List<string> { "Achievement", "Badge", "Credential", "Recognition", "Milestone", "Honor", "Certification", "Distinction" };

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

        public string OwnerId { get; set; } = string.Empty;
    }
}