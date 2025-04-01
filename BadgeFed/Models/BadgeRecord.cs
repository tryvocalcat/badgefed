namespace BadgeFed.Models
{
    public class BadgeRecord
    {
        public long Id { get; set; }
        public string Title { get; set; } = "";
        public string IssuedBy { get; set; } = "";
        public string Description { get; set; } = "";
        public string Image { get; set; } = "";
        public string ImageAltText { get; set; } = "";
        public string EarningCriteria { get; set; } = "";
        
        public DateTime IssuedOn { get; set; }
        
        [System.Text.Json.Serialization.JsonIgnore]
        public string IssuedToEmail { get; set; } = "";
        
        public string IssuedToName { get; set; } = "";

        public string IssuedToSubjectUri { get; set; } = "";
     
        public DateTime? AcceptedOn { get; set; }
        public DateTime? LastUpdated { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public string FingerPrint { get; set; } = "";

        [System.Text.Json.Serialization.JsonIgnore]
        public string AcceptKey { get; set; } = "";

        [System.Text.Json.Serialization.JsonIgnore]
        public Badge Badge { get; set; } = new Badge();

        [System.Text.Json.Serialization.JsonIgnore]
        public Actor Actor { get; set; } = new Actor();

        [System.Text.Json.Serialization.JsonIgnore]
        public string IssuerFediverseHandle { get {
            return "@" + Actor.Username + "@" + Actor.Domain;
        }}
    }
}