using System.ComponentModel.DataAnnotations;

namespace BadgeFed.Models
{
    public class Registration
    {
        public int Id { get; set; }
        
        [Required]
        public string FormData { get; set; } = string.Empty; // JSON serialized form data
        
        public string IpAddress { get; set; } = string.Empty;
        
        public string UserAgent { get; set; } = string.Empty;
        
        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
        
        public bool IsReviewed { get; set; } = false;
        
        public bool IsApproved { get; set; } = false;
        
        public string? ReviewNotes { get; set; }
        
        public DateTime? ReviewedAt { get; set; }
        
        public string? ReviewedBy { get; set; }
        
        public string? Email { get; set; } // Extracted from form data for easy querying
        
        public string? Name { get; set; } // Extracted from form data for easy querying
    }
}
