using System.Text.Json;
using BadgeFed.Services;
using Microsoft.Extensions.Logging;

namespace ActivityPubDotNet.Core
{
    public class ExternalBadgeService
    {
        public ILogger? Logger { get; set; }

        private readonly LocalDbService _localDbService;

        public ExternalBadgeService(LocalDbService localDbService)
        {
            _localDbService = localDbService;
        }

        public Task ProcessExternalBadge(ActivityPubNote objectNote)
        {
            if (objectNote!.InReplyTo != null)
            {
                // We don't do anything
                return Task.CompletedTask;
            }

            if (objectNote.BadgeMetadata == null)
            {
                // We don't do anything
                return Task.CompletedTask;
            }

            var grant = objectNote.BadgeMetadata;

            var existingLocal = _localDbService.GetGrantByNoteId(grant.NoteId);

            if (existingLocal != null)
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