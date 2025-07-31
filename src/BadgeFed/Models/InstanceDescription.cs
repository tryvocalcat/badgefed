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
    }
}
