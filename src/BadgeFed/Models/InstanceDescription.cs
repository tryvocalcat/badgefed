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
        
        [MaxLength(1000)]
        public string Purpose { get; set; } = "";
        
        [MaxLength(300)]
        public string ContactInfo { get; set; } = "";
        
        [MaxLength(255)]
        public string Domain { get; set; } = "";
        
        public string CustomLandingPageHtml { get; set; } = "";
        
        public bool IsEnabled { get; set; } = false;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
