using System.ComponentModel.DataAnnotations;

namespace BadgeFed.Models
{
    public class Badge
    {
        public long Id { get; set; }

        [Required(ErrorMessage = "Title is required")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Title must be between 2 and 100 characters")]
        public string Title { get; set; } = "";

        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
        public string Description { get; set; } = "";

        [Range(1, long.MaxValue, ErrorMessage = "Please select an issuer")]
        public long IssuedBy { get; set; }

        public Actor? Issuer { get; set; }

        public string Image { get; set; } = "";

        [StringLength(1500, ErrorMessage = "Image alt text cannot exceed 1500 characters")]
        public string ImageAltText { get; set; } = "";

        [Required(ErrorMessage = "Earning criteria is required")]
        [StringLength(500, ErrorMessage = "Earning criteria cannot exceed 500 characters")]
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

        [Url(ErrorMessage = "Please enter a valid URL")]
        public string InfoUri { get; set; } = "";

        public bool IsCertificate { get; set; } = false;

        public string OwnerId { get; set; } = string.Empty;
    }
}