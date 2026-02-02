using System.ComponentModel.DataAnnotations;

namespace BadgeFed.Models
{
    public class TokenGrant
    {
        public long Id { get; set; }

        [Required]
        public string Token { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public string ShortCode { get; set; } = string.Empty;

        [Required]
        public long BadgeId { get; set; }

        public Badge? Badge { get; set; }

        [Required]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        public DateTime? EnabledAt { get; set; }

        public int? MaxRedemptions { get; set; } // null = infinite

        public int RedeemedCount { get; set; } = 0;

        public bool IsActive { get; set; } = true;

        public string CreatedBy { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        // Calculated properties
        public bool IsEnabled => IsActive && (EnabledAt == null || EnabledAt <= DateTime.UtcNow);

        public bool HasRedemptionsLeft => MaxRedemptions == null || RedeemedCount < MaxRedemptions.Value;

        public bool CanRedeem => IsEnabled && HasRedemptionsLeft;

        public string PublicUrl => $"/get/{ShortCode}";
    }

    public class TokenGrantRedemption
    {
        public long Id { get; set; }

        [Required]
        public long TokenGrantId { get; set; }

        public TokenGrant? TokenGrant { get; set; }

        [Required]
        public long BadgeRecordId { get; set; }

        public BadgeRecord? BadgeRecord { get; set; }

        [Required]
        public string RecipientName { get; set; } = string.Empty;

        [EmailAddress]
        public string? RecipientEmail { get; set; }

        [Url]
        public string? RecipientProfileUri { get; set; }

        public string? IpAddress { get; set; }

        public string? UserAgent { get; set; }

        public DateTime RedeemedAt { get; set; } = DateTime.UtcNow;
    }
}