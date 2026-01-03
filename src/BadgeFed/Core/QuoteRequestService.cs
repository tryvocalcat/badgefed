using System.Text.Json;
using BadgeFed.Services;
using BadgeFed.Models;
using Microsoft.Extensions.Logging;
using ActivityPubDotNet.Core;
using System.Text.Json.Serialization;

namespace BadgeFed.Core
{
    public class QuoteRequestService
    {
        public ILogger? Logger { get; set; }
        private readonly LocalScopedDb _db;

        public QuoteRequestService(LocalScopedDb db)
        {
            _db = db;
        }

        public async Task ProcessQuoteRequest(InboxMessage message)
        {
            if (message == null || !message.IsQuoteRequest())
            {
                Logger?.LogWarning("Invalid quote request message");
                return;
            }

            try
            {
                Logger?.LogInformation($"Processing quote request from {message.Actor} for object {message.Object}");

                // For automatic approval, we'll immediately accept all quote requests
                // In a production system, you might want to add filtering logic here
                await AutoApproveQuoteRequest(message);
                
                // Add to notifications system
                var localDbService = _db as LocalDbService;
                localDbService?.InsertRecentActivityLog("Quote request received", 
                    $"Auto-approved quote from {message.Actor}", 
                    message.Object?.ToString());
                
                Logger?.LogInformation($"Quote request automatically approved for {message.Actor}");
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, $"Error processing quote request from {message.Actor}");
            }
        }

        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        private async Task AutoApproveQuoteRequest(InboxMessage message)
        {
            try
            {
                // Get the object URL being quoted
                var noteUrl = message.Object?.ToString();
                if (string.IsNullOrEmpty(noteUrl))
                {
                    Logger?.LogWarning("Quote request missing object URL");
                    return;
                }

                // Find the actor that owns the quoted object
                var actor = GetActorForQuotedObject(noteUrl);
                if (actor == null)
                {
                    Logger?.LogWarning($"Could not find actor for quoted object: {noteUrl}");
                    return;
                }

                // Create Accept response
                var objectId = Guid.NewGuid().ToString();
                var quoteId = $"https://{actor.Domain}/activities/accept/{objectId}";
                var stampId = $"https://{actor.Domain}/stamps/{objectId}";

                var acceptResponse = new QuoteAcceptResponse
                {
                    Context = new object[]
                    {
                        "https://www.w3.org/ns/activitystreams",
                        new Dictionary<string, object>
                        {
                            {"QuoteRequest", "https://w3id.org/fep/044f#QuoteRequest"}
                        }
                    },
                    Type = "Accept",
                    Id = quoteId,
                    Actor = actor.Uri?.ToString(),
                    To = message.Actor,
                    Object = message,
                    Result = stampId // This acts as the QuoteAuthorization
                };

                // Send the Accept response
                await SendAcceptResponse(acceptResponse, message.Actor, actor);

                Logger?.LogInformation($"Sent Accept response to {message.Actor} for quote request {message.Id}");
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Error auto-approving quote request");
            }
        }

        private Actor? GetActorForQuotedObject(string noteUrl)
        {
            try
            {
                var badgeRecord = _db.GetGrantByNoteId(noteUrl);

                if (badgeRecord == null)
                {
                    Logger?.LogInformation($"Cannot find badge record for quoted object: {noteUrl}");
                    return _db.GetMainActor();
                }

                var actorUrl = badgeRecord.IssuedBy;
                
                var actor = _db.GetActorByUri(actorUrl);

                Logger?.LogInformation($"Found actor {actor?.Id} for quoted object: {noteUrl} in {actorUrl}");

                return actor ?? _db.GetMainActor();
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, $"Error getting actor for quoted object: {noteUrl}");
                return _db.GetMainActor();
            }
        }

        private async Task SendAcceptResponse(QuoteAcceptResponse acceptResponse, string requesterActorUrl, Actor respondingActor)
        {
            try
            {
                var actorHelper = new ActorHelper(respondingActor.PrivateKeyPemClean!, respondingActor.KeyId, Logger);

                // Fetch the requester's actor information to get their inbox
                var requesterActor = await actorHelper.FetchActorInformationAsync(requesterActorUrl);

                var acceptJson = JsonSerializer.Serialize(acceptResponse, SerializerOptions);
                
                Logger?.LogInformation($"Sending Accept response to {requesterActor.Inbox}: {acceptJson}");
                
                await actorHelper.SendPostSignedRequest(acceptJson, new Uri(requesterActor.Inbox));

                Logger?.LogInformation($"Successfully sent Accept response to {requesterActorUrl}");
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, $"Error sending Accept response to {requesterActorUrl}");
            }
        }
    }

    public class QuoteAcceptResponse
    {
        [JsonPropertyName("@context")]
        public object Context { get; set; } = default!;

        [JsonPropertyName("type")]
        public string Type { get; set; } = "Accept";

        [JsonPropertyName("id")]
        public string Id { get; set; } = default!;

        [JsonPropertyName("actor")]
        public string Actor { get; set; } = default!;

        [JsonPropertyName("to")]
        public string To { get; set; } = default!;

        [JsonPropertyName("object")]
        public object Object { get; set; } = default!;

        [JsonPropertyName("result")]
        public string Result { get; set; } = default!;
    }
}