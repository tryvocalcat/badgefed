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
                Logger?.LogInformation($"Processing reply for note: {objectNote.Id}");
                return _repliesService.ProcessReply(objectNote);
            }

            if (objectNote.Attachment != null && objectNote.Attachment.Count > 0)
            {
                Logger?.LogInformation($"Processing external badge for note: {objectNote.Id}");
                _externalBadgeService.Logger = Logger;
                return _externalBadgeService.ProcessExternalBadge(objectNote);
            }

            Logger?.LogInformation($"No action for note: {objectNote.Id}");

            return Task.CompletedTask;
        }
    }
}