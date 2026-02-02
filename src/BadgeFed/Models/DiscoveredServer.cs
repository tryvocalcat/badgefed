using System.Text.Json;

namespace BadgeFed.Models
{
    public class DiscoveredServer
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Categories { get; set; } = string.Empty; // JSON array stored as string
        public string Admin { get; set; } = string.Empty;
        public string Actor { get; set; } = string.Empty;
        public bool IsFollowed { get; set; } = false;
        public DateTime? FollowedAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public List<string> GetCategories()
        {
            if (string.IsNullOrEmpty(Categories))
                return new List<string>();
            
            try
            {
                return JsonSerializer.Deserialize<List<string>>(Categories) ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }

        public void SetCategories(List<string> categories)
        {
            Categories = JsonSerializer.Serialize(categories);
        }
    }

    public class ServerFromJson
    {
        public string name { get; set; } = string.Empty;
        public string url { get; set; } = string.Empty;
        public string description { get; set; } = string.Empty;
        public List<string> categories { get; set; } = new();
        public string admin { get; set; } = string.Empty;
        public string actor { get; set; } = string.Empty;
    }
}
