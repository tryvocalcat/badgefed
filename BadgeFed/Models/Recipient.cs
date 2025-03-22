using System;

namespace BadgeFed.Models
{
    public class Recipient
    {
        public long Id { get; set; }
        
        public string Name { get; set; } = null!;
        
        public string Email { get; set; } = null!;
        
        public string? ProfileUri { get; set; }

        public bool IsActivityPubActor { get; set; } = false;
    }
}