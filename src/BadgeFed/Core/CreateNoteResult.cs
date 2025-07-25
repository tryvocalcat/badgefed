using BadgeFed.Models;

namespace ActivityPubDotNet.Core
{
    public enum CreateNoteResultType
    {
        /// <summary>
        /// An error occurred during processing
        /// </summary>
        Error,
        
        /// <summary>
        /// The note was processed as a reply to an existing badge
        /// </summary>
        Reply,
        
        /// <summary>
        /// The note was not processed (no action taken)
        /// </summary>
        NotProcessed,
        
        /// <summary>
        /// A valid external badge was successfully processed
        /// </summary>
        ExternalBadgeProcessed
    }

    public class CreateNoteResult
    {
        public CreateNoteResultType Type { get; set; }
        public BadgeRecord? BadgeRecord { get; set; }
        public ActivityPubNote? Note { get; set; }
        public string? ErrorMessage { get; set; }
        public Exception? Exception { get; set; }

        public static CreateNoteResult Error(string message, Exception? exception = null)
        {
            return new CreateNoteResult
            {
                Type = CreateNoteResultType.Error,
                ErrorMessage = message,
                Exception = exception
            };
        }

        public static CreateNoteResult Reply()
        {
            return new CreateNoteResult
            {
                Type = CreateNoteResultType.Reply
            };
        }

        public static CreateNoteResult NotProcessed()
        {
            return new CreateNoteResult
            {
                Type = CreateNoteResultType.NotProcessed
            };
        }

        public static CreateNoteResult ExternalBadgeProcessed(BadgeRecord badgeRecord, ActivityPubNote? note = null)
        {
            return new CreateNoteResult
            {
                Type = CreateNoteResultType.ExternalBadgeProcessed,
                BadgeRecord = badgeRecord,
                Note = note
            };
        }
    }
}
