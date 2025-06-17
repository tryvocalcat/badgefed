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

        private readonly LocalDbService _localDbService;

        public CreateNoteService(RepliesService repliesService, ExternalBadgeService externalBadgeService, LocalDbService localDbService)
        {
            _externalBadgeService = externalBadgeService;
            _repliesService = repliesService;
            _localDbService = localDbService;
        }

        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        public async Task ProcessAnnounce(InboxMessage message)
        {
            Logger?.LogInformation($"Processing announce for actor: {message.Actor}");

            if (message.Object == null)
            {
                Logger?.LogError("Announce message has no object");
                return;
            }

            // The object in an Announce is typically a URL to the original note
            var originalNoteUrl = message.Object.ToString();
            
            if (string.IsNullOrEmpty(originalNoteUrl))
            {
                Logger?.LogError("Announce message object is null or empty");
                return;
            }

            Logger?.LogInformation($"Fetching original note from: {originalNoteUrl}");

            try
            {
                // We need to fetch the original note from the URL
                // For this, we'll need an actor to make the signed request
                // Let's get the main actor to make the request
                var mainActor = _localDbService.GetActorByFilter("IsMain = 1");

                if (mainActor == null)
                {
                    Logger?.LogError("No main actor found to fetch the original note");
                    return;
                }

                var actorHelper = new ActorHelper(mainActor.PrivateKeyPemClean!, mainActor.KeyId, Logger);
                var noteJson = await actorHelper.SendGetSignedRequest(new Uri(originalNoteUrl));

                // Deserialize the fetched note
                var fetchedNote = JsonSerializer.Deserialize<ActivityPubNote>(noteJson, SerializerOptions);

                if (fetchedNote == null)
                {
                    Logger?.LogError($"Failed to deserialize fetched note from {originalNoteUrl}");
                    return;
                }

                if (fetchedNote.Attachment != null && fetchedNote.Attachment.Count > 0)
                {
                    Logger?.LogInformation($"Processing external badge for announced note: {fetchedNote.Id}");
                    _externalBadgeService.Logger = Logger;
                    await _externalBadgeService.ProcessExternalBadge(fetchedNote);
                }
                else
                {
                    Logger?.LogInformation($"No specific action for announced note: {fetchedNote.Id}");
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError($"Error processing announce: {ex.Message}");
            }
        }

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