using System;

namespace BadgeFed.Models
{
    public class User
    {
        public string Id { get; set; }
        
        public string Email { get; set; }
        
        public string GivenName { get; set; }
        
        public string Surname { get; set; }
        
        public DateTime CreatedAt { get; set; }
        
        public string Provider { get; set; }
        
        public string Role { get; set; } = "manager";

        public bool IsActive { get; set; }
    }
}