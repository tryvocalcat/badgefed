namespace BadgeFed.Models
{
    public class BadgeDefinition
    {
        public string Title { get; set; } = "";
        
        public string Description { get; set; } = "";

        public Actor IssuedBy { get; set; } = new Actor();
        
        public string Image { get; set; } = "";
        
        public string EarningCriteria { get; set; } = "";
    }
}