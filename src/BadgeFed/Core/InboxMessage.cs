namespace ActivityPubDotNet.Core
{
    public class InboxMessage
    {
        public string? Id { get; set; }
        public string? Actor { get; set; }
        public string? Type { get; set; }

        public object? Object { get; set; }
        
        /// <summary>
        /// For QuoteRequest activities, this contains the URL of the note that is doing the quoting (interactingObject).
        /// Can be a string URL or an object with an id property.
        /// </summary>
        public ActivityPubObject? Instrument { get; set; }

        public bool IsFollow() => Type == "Follow";
        public bool IsUndoFollow() => Type == "Undo";
        public bool IsCreateActivity() => Type == "Create";
        public bool IsDelete() => Type == "Delete";
        public bool IsQuoteRequest() => Type == "QuoteRequest";

        public bool IsAnnounce() => Type == "Announce";
    }
}