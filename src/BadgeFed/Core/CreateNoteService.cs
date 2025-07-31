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

        private readonly LocalDbFactory _localDbFactory;

        public CreateNoteService(RepliesService repliesService, ExternalBadgeService externalBadgeService, LocalDbFactory localDbFactory)
        {
            _externalBadgeService = externalBadgeService;
            _repliesService = repliesService;
            _localDbFactory = localDbFactory;
        }

        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        public async Task<CreateNoteResult> ProcessAnnounce(InboxMessage message)
        {
            Logger?.LogInformation($"Processing announce for actor: {message.Actor}");

            if (message.Object == null)
            {
                Logger?.LogError("Announce message has no object");
                return CreateNoteResult.Error("Announce message has no object");
            }

            // The object in an Announce is typically a URL to the original note
            var originalNoteUrl = message.Object.ToString();
            
            if (string.IsNullOrEmpty(originalNoteUrl))
            {
                Logger?.LogError("Announce message object is null or empty");
                return CreateNoteResult.Error("Announce message object is null or empty");
            }

            Logger?.LogInformation($"Fetching original note from: {originalNoteUrl}");

            try
            {
                // We need to fetch the original note from the URL
                // For this, we'll need an actor to make the signed request
                // Let's get the main actor to make the request
                
                // Get the domain from the URL
                Uri originalNoteUri;
                if (!Uri.TryCreate(originalNoteUrl, UriKind.Absolute, out originalNoteUri))
                {
                    Logger?.LogError($"Invalid note URI format: {originalNoteUrl}");
                    return CreateNoteResult.Error("Invalid note URI format");
                }
                
                var localDbService = _localDbFactory.GetInstance(originalNoteUri);
                var mainActor = localDbService.GetMainActor();

                if (mainActor == null)
                {
                    Logger?.LogError("No main actor found to fetch the original note");
                    return CreateNoteResult.Error("No main actor found to fetch the original note");
                }

                var actorHelper = new ActorHelper(mainActor.PrivateKeyPemClean!, mainActor.KeyId, Logger);
                var noteJson = await actorHelper.SendGetSignedRequest(new Uri(originalNoteUrl));

                // Deserialize the fetched note
                var fetchedNote = JsonSerializer.Deserialize<ActivityPubNote>(noteJson, SerializerOptions);

                if (fetchedNote == null)
                {
                    Logger?.LogError($"Failed to deserialize fetched note from {originalNoteUrl}");
                    return CreateNoteResult.Error($"Failed to deserialize fetched note from {originalNoteUrl}");
                }

                if (fetchedNote.Attachment != null && fetchedNote.Attachment.Count > 0)
                {
                    Logger?.LogInformation($"Processing external badge for announced note: {fetchedNote.Id}");
                    _externalBadgeService.Logger = Logger;
                    var badgeRecord = await _externalBadgeService.ProcessExternalBadge(fetchedNote);
                    
                    if (badgeRecord != null)
                    {
                        return CreateNoteResult.ExternalBadgeProcessed(badgeRecord, fetchedNote);
                    }
                    else
                    {
                        Logger?.LogInformation($"No valid external badge found in announced note: {fetchedNote.Id}");
                        return CreateNoteResult.NotProcessed();
                    }
                }
                else
                {
                    Logger?.LogInformation($"No specific action for announced note: {fetchedNote.Id}");
                    return CreateNoteResult.NotProcessed();
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError($"Error processing announce: {ex.Message}");
                return CreateNoteResult.Error($"Error processing announce: {ex.Message}", ex);
            }
        }

        public async Task<CreateNoteResult> ProcessMessage(InboxMessage message)
        {
            var objectNote = JsonSerializer.Deserialize<ActivityPubNote>(JsonSerializer.Serialize(message!.Object!, SerializerOptions), SerializerOptions);

            if (objectNote == null)
            {
                Logger?.LogError("Failed to deserialize object note");
                return CreateNoteResult.Error("Failed to deserialize object note");
            }

            if (objectNote!.InReplyTo != null)
            {
                Logger?.LogInformation($"Processing reply for note: {objectNote.Id}");
                await _repliesService.ProcessReply(objectNote);
                return CreateNoteResult.Reply();
            }

            if (objectNote.Attachment != null && objectNote.Attachment.Count > 0)
            {
                Logger?.LogInformation($"Processing external badge for note: {objectNote.Id}");
                _externalBadgeService.Logger = Logger;
                var badgeRecord = await _externalBadgeService.ProcessExternalBadge(objectNote);
                
                if (badgeRecord != null)
                {
                    return CreateNoteResult.ExternalBadgeProcessed(badgeRecord, objectNote);
                }
                else
                {
                    Logger?.LogInformation($"No valid external badge found in note: {objectNote.Id}");
                    return CreateNoteResult.NotProcessed();
                }
            }

            Logger?.LogInformation($"No action for note: {objectNote.Id}");
            return CreateNoteResult.NotProcessed();
        }
    }
}