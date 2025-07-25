namespace BadgeFed.Models
{
    public class Follower
    {
        public string FollowerUri { get; set; } = string.Empty;
        public string Domain { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        public Actor Parent { get; set; } = new Actor();

        public string AvatarUri { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;
    }
}