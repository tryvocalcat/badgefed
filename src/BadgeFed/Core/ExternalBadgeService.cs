using System.Text.Json;
using BadgeFed.Models;
using BadgeFed.Services;
using Microsoft.Extensions.Logging;

namespace ActivityPubDotNet.Core
{
    public class ExternalBadgeService
    {
        public ILogger? Logger { get; set; }
        private readonly LocalDbService _localDbService;
        private readonly OpenBadgeImportService _openBadgeImportService;

        public ExternalBadgeService(LocalDbService localDbService)
        {
            _localDbService = localDbService;
            _openBadgeImportService = new OpenBadgeImportService(localDbService);
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
                // Try to deserialize as ActivityPub badge first
                var serializedGrant = JsonSerializer.Serialize(grant, options);

                // To be deprecated -- avoiding creating custom spec
                if (serializedGrant.Contains("https://vocalcat.com/badgefed/1.0"))
                {
                    var badgeRecord = JsonSerializer.Deserialize<BadgeRecord>(serializedGrant, options);

                    if (badgeRecord?.Context == "https://vocalcat.com/badgefed/1.0")
                    {
                        return await ProcessActivityPubBadge(badgeRecord, objectNote.Id);
                    }
                }
                
                // Trying openbadges v2
                if (serializedGrant.Contains("https://w3id.org/openbadges/v2"))
                {
                    _openBadgeImportService.Logger = Logger;
                    return await _openBadgeImportService.ImportOpenBadge(serializedGrant);
                }

                // TODO: Implement OpenBadges v3
                // TODO: Implement https://w3c.github.io/vc-data-model/

                Logger?.LogInformation("Unrecognized badge format");
                return null;
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
            var existingLocal = _localDbService.GetGrantByNoteId(badgeRecord.NoteId);

            if (existingLocal != null)
            {
                Logger?.LogInformation($"Grant already exists in local database: {existingLocal.Id}");
                return existingLocal;
            }

            badgeRecord.IsExternal = true;
            Logger?.LogInformation($"Creating new ActivityPub badge record in local database");
            _localDbService.CreateBadgeRecord(badgeRecord);

            return badgeRecord;
        }
    }
}