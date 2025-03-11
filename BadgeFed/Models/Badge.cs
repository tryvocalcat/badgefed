namespace BadgeFed.Models
{
    public class Badge
    {
        public long Id { get; set; }
        public string Title { get; set; } = "";
        public string IssuedBy { get; set; } = "";
        public string Description { get; set; } = "";
        public string Image { get; set; } = "";
        public string EarningCriteria { get; set; } = "";
        public string IssuedUsing { get; set; } = "";
        public DateTime IssuedOn { get; set; }
        public string IssuedTo { get; set; } = "";
        public DateTime? AcceptedOn { get; set; }
        public DateTime? LastUpdated { get; set; }
        public string FingerPrint { get; set; } = "";

        public BadgeDefinition BadgeDefinition { get; set; } = new BadgeDefinition();
    }
}