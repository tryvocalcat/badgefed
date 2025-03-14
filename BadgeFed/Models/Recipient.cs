using System;

namespace BadgeFed.Models
{
    public class Recipient
    {
        public long Id { get; set; }
        
        public string FullName { get; set; } = null!;
        
        public string Email { get; set; } = null!;
        
        public string? FediverseHandle { get; set; }
        
        public string ProfileUri { get; set; } = null!;

        public static string GetAssignedToType(string assignedTo)
        {
            if (assignedTo.StartsWith("https://") || assignedTo.StartsWith("http://"))
            {
                return "profileuri";
            }
            else if (assignedTo.StartsWith("@") && assignedTo.IndexOf('@', 1) > 1)
            {
                return "fediverse";
            }
            else if (assignedTo.Count(c => c == '@') == 1 && assignedTo.IndexOf('@') > 0)
            {
                return "email";
            }
            else
            {
                return "fullname";
            }

        }
    }
}