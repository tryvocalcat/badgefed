using System.ComponentModel.DataAnnotations;

namespace BadgeFed.Models
{
    public class InstanceDescription
    {
        public int Id { get; set; }
        
        [MaxLength(100)]
        public string Name { get; set; } = "";
        
        [MaxLength(500)]
        public string Description { get; set; } = "";

        public string CustomLandingPageHtml { get; set; } = "";
        
        [MaxLength(1000)]
        public string Purpose { get; set; } = "";
        
        [MaxLength(300)]
        public string ContactInfo { get; set; } = "";
        
        public bool IsEnabled { get; set; } = false;
        
        // New fields for landing page management
        public string LandingPageType { get; set; } = "default"; // "default", "custom_html", "static_page"
        public string StaticPageFilename { get; set; } = ""; // If using static page redirect
    }

    public class StaticPage
    {
        public int Id { get; set; }
        public string Filename { get; set; } = "";
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public long FileSize { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
