using System;
using System.Text.Json;
using System.Collections.Generic;

namespace BadgeFed.Models
{
    public class Recipient
    {
        public long Id { get; set; }
        
        public string Name { get; set; } = null!;
        
        public string Email { get; set; } = null!;
        
        public string? ProfileUri { get; set; }

        public bool IsActivityPubActor { get; set; } = false;

        // Profile customization fields
        public string? DisplayName { get; set; }
        public string? Bio { get; set; }
        public string? AvatarPath { get; set; }
        public string? Slug { get; set; }
        public string ProfileTemplate { get; set; } = "professional";
        public string? Theme { get; set; }
        public string? ProfileLinks { get; set; }
        public string? CustomHeadline { get; set; }
        public bool IsPublic { get; set; } = true;
        public long? PrimaryRecipientId { get; set; }

        // Computed property for deserialized profile links
        public List<ProfileLink> ProfileLinksList =>
            string.IsNullOrEmpty(ProfileLinks)
                ? new()
                : JsonSerializer.Deserialize<List<ProfileLink>>(ProfileLinks) ?? new();
    }
}