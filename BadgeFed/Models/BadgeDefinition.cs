namespace BadgeFed.Models
{
    public class BadgeDefinition
    {
        public long Id { get; set; }

        public string Title { get; set; } = "";
        
        public string Description { get; set; } = "";

        public long IssuedBy { get; set; }
        
        public string Image { get; set; } = "";
        
        public string EarningCriteria { get; set; } = "";
    }
}