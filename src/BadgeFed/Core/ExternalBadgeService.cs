using System.Text.Json;
using BadgeFed.Models;
using BadgeFed.Services;
using Microsoft.Extensions.Logging;

namespace ActivityPubDotNet.Core
{
    public class ExternalBadgeService
    {
        public ILogger? Logger { get; set; }
        private readonly LocalDbFactory _localDbFactory;
        private readonly BadgeProcessor _badgeProcessor;

        public ExternalBadgeService(LocalDbFactory localDbFactory, BadgeProcessor badgeProcessor)
        {
            _localDbFactory = localDbFactory;
            _badgeProcessor = badgeProcessor;
        }

        public async Task<BadgeRecord?> ProcessExternalBadge(ActivityPubNote objectNote)
        {
            Logger?.LogInformation($"Processing external badge for note: {objectNote.Id}");

            if (objectNote!.InReplyTo != null)
            {
                Logger?.LogInformation("Reply detected, no action taken.");
                return null;
            }

            if (objectNote.Attachment == null || objectNote.Attachment.Count == 0)
            {
                Logger?.LogInformation("No attachment detected, no action taken.");
                return null;
            }

            var grant = objectNote.Attachment.FirstOrDefault();

            if (grant == null)
            {
                Logger?.LogInformation("No grant detected, no action taken.");
                return null;
            }

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };

            try {
                var db = _localDbFactory.GetInstance(new Uri(objectNote.Id));
                var _openBadgeImportService = new OpenBadgeImportService(db);

                // Try to deserialize as ActivityPub badge first
                var serializedGrant = JsonSerializer.Serialize(grant, options);

                BadgeRecord? processedBadgeRecord = null;

                // To be deprecated -- avoiding creating custom spec
                if (serializedGrant.Contains("https://vocalcat.com/badgefed/1.0"))
                {
                    var badgeRecord = JsonSerializer.Deserialize<BadgeRecord>(serializedGrant, options);

                    if (badgeRecord?.Context == "https://vocalcat.com/badgefed/1.0")
                    {
                        processedBadgeRecord = await ProcessActivityPubBadge(badgeRecord, objectNote.Id);
                    }
                }
                
                // Trying openbadges v2
                if (serializedGrant.Contains("https://w3id.org/openbadges/v2"))
                {
                    _openBadgeImportService.Logger = Logger;
                    processedBadgeRecord = await _openBadgeImportService.ImportOpenBadge(serializedGrant);
                }

                // If we successfully processed a badge, announce it
                if (processedBadgeRecord != null)
                {
                    Logger?.LogInformation($"Successfully processed external badge, creating announcement for record ID: {processedBadgeRecord.Id}");
                    try
                    {
                        await _badgeProcessor.AnnounceGrantByMainActor(processedBadgeRecord);
                        Logger?.LogInformation($"Successfully announced external badge: {processedBadgeRecord.NoteId}");
                    }
                    catch (Exception announceEx)
                    {
                        Logger?.LogError($"Failed to announce external badge: {announceEx.Message}");
                        // Don't fail the whole process if announce fails
                    }
                }

                // TODO: Implement OpenBadges v3
                // TODO: Implement https://w3c.github.io/vc-data-model/

                if (processedBadgeRecord == null)
                {
                    Logger?.LogInformation("Unrecognized badge format");
                }

                return processedBadgeRecord;
            }
            catch (JsonException ex)
            {
                Logger?.LogError($"Failed to deserialize badge record: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                Logger?.LogError($"An unexpected error occurred: {ex.Message}");
                return null;
            }
        }

        private async Task<BadgeRecord?> ProcessActivityPubBadge(BadgeRecord badgeRecord, string noteId)
        {
            // Extract domain from noteId to get the appropriate LocalDbService
            Uri noteUri;
            if (!Uri.TryCreate(badgeRecord.NoteId, UriKind.Absolute, out noteUri))
            {
                Logger?.LogError($"Invalid note URI format: {badgeRecord.NoteId}");
                return null;
            }
            
            var localDbService = _localDbFactory.GetInstance(noteUri);
            var existingLocal = localDbService.GetGrantByNoteId(badgeRecord.NoteId);

            if (existingLocal != null)
            {
                Logger?.LogInformation($"Grant already exists in local database: {existingLocal.Id}");
                return existingLocal;
            }

            badgeRecord.IsExternal = true;
            Logger?.LogInformation($"Creating new ActivityPub badge record in local database");
            localDbService.CreateBadgeRecord(badgeRecord);

            return badgeRecord;
        }
    }
}