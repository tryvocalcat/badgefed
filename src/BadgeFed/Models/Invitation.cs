using System;

namespace BadgeFed.Models
{
    public class Invitation
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        public string Email { get; set; }
        
        public string InvitedBy { get; set; } // User ID who created the invitation
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime ExpiresAt { get; set; }
        
        public string? AcceptedBy { get; set; } // User ID who accepted the invitation
        
        public DateTime? AcceptedAt { get; set; }
        
        public bool IsActive { get; set; } = true;
        
        public string Role { get; set; } = "manager"; // Role to assign to the invited user
        
        public string? Notes { get; set; } // Optional notes about the invitation
        
        public string InvitationUrl => $"/admin/login?invitationCode={Id}";
        
        public bool IsExpired => DateTime.UtcNow > ExpiresAt;
        
        public bool IsUsed => !string.IsNullOrEmpty(AcceptedBy);
        
        public bool IsValid => IsActive && !IsExpired && !IsUsed;
    }
}
