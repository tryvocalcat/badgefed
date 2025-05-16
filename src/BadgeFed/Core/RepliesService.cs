using System.Text.Json;
using BadgeFed.Services;
using Microsoft.Extensions.Logging;

namespace ActivityPubDotNet.Core
{
    public class RepliesService
    {
        public ILogger? Logger { get; set; }

        private readonly LocalDbService _localDbService;

        public RepliesService(LocalDbService localDbService)
        {
            _localDbService = localDbService;
        }

        public Task ProcessReply(ActivityPubNote objectNote)
        {
            if (objectNote!.InReplyTo == null)
            {
                // We don't do anything
                return Task.CompletedTask;
            }
            
            // try to get the id is the last part of inReplyTo
            var noteId = objectNote!.InReplyTo?.Split('/').LastOrDefault();

            var grant = _localDbService.GetGrantByNoteId(noteId);

            if (grant == null)
            {
                // We don't do anything
                return Task.CompletedTask;
            }

            // Maybe is a reply (aka a comment)
            _localDbService.UpsertBadgeComment(grant.Id, objectNote.Id, objectNote.AttributedTo);

            return Task.CompletedTask;
        }
    }
}