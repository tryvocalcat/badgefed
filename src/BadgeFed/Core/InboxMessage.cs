namespace ActivityPubDotNet.Core
{
    public class InboxMessage
    {
        public string? Id { get; set; }
        public string? Actor { get; set; }
        public string? Type { get; set; }

        public object? Object { get; set; }

        public bool IsFollow() => Type == "Follow";
        public bool IsUndoFollow() => Type == "Undo";
        public bool IsCreateActivity() => Type == "Create";
        public bool IsDelete() => Type == "Delete";

        public bool IsAnnounce() => Type == "Announce";
    }
}