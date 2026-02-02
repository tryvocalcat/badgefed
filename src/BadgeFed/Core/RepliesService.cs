using System.Text.Json;
using BadgeFed.Services;
using Microsoft.Extensions.Logging;

namespace ActivityPubDotNet.Core
{
    public class RepliesService
    {
        public ILogger? Logger { get; set; }

        public RepliesService()
        {
        }

        public Task ProcessReply(ActivityPubNote objectNote, LocalDbService localDbService)
        {
            if (objectNote!.InReplyTo == null)
            {
                // We don't do anything
                return Task.CompletedTask;
            }
            
            // try to get the id is the last part of inReplyTo
            var noteId = objectNote!.InReplyTo?.Split('/').LastOrDefault();

            try
            {
                Uri noteUri;
                if (!Uri.TryCreate(objectNote.InReplyTo, UriKind.Absolute, out noteUri))
                {
                    Logger?.LogError($"Invalid note URI format: {objectNote.InReplyTo}");
                    return Task.CompletedTask;
                }

                var grant = localDbService.GetGrantByNoteId(noteId);

                if (grant == null)
                {
                    // We don't do anything
                    return Task.CompletedTask;
                }

                // Maybe is a reply (aka a comment)
                localDbService.UpsertBadgeComment(grant.Id, objectNote.Id, objectNote.AttributedTo);
            }
            catch (Exception ex)
            {
                Logger?.LogError($"Error processing reply: {ex.Message}");
            }

            return Task.CompletedTask;
        }
    }
}