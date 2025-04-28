using System.Text.Json;
using BadgeFed.Services;
using Microsoft.Extensions.Logging;

namespace ActivityPubDotNet.Core
{
    public class CreateNoteService
    {
        public ILogger? Logger { get; set; }

        private readonly RepliesService _repliesService;

        private readonly ExternalBadgeService _externalBadgeService;

        public CreateNoteService(RepliesService repliesService, ExternalBadgeService externalBadgeService)
        {
            _externalBadgeService = externalBadgeService;
            _repliesService = repliesService;
        }

        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        public Task ProcessMessage(InboxMessage message)
        {
            var objectNote = JsonSerializer.Deserialize<ActivityPubNote>(JsonSerializer.Serialize(message!.Object!, SerializerOptions), SerializerOptions);

            if (objectNote == null)
            {
                Logger?.LogError("Failed to deserialize object note");
                return Task.CompletedTask;
            }


            if (objectNote!.InReplyTo != null)
            {
                // We don't do anything
                return _repliesService.ProcessReply(objectNote);
            }

            if (objectNote.BadgeMetadata != null)
            {
                return _externalBadgeService.ProcessExternalBadge(objectNote);
            }

            return Task.CompletedTask;
        }
    }
}