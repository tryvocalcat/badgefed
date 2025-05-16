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

        public ExternalBadgeService(LocalDbService localDbService)
        {
            _localDbService = localDbService;
        }

        public Task ProcessExternalBadge(ActivityPubNote objectNote)
        {
            Logger?.LogInformation($"Processing external badge for note: {objectNote.Id}");

            if (objectNote!.InReplyTo != null)
            {
                Logger?.LogInformation("Reply detected, no action taken.");
                return Task.CompletedTask;
            }

            if (objectNote.Attachment == null || objectNote.Attachment.Count == 0)
            {
                Logger?.LogInformation("No attachment detected, no action taken.");
                return Task.CompletedTask;
            }

            var grant = objectNote.Attachment.FirstOrDefault();

            if (grant == null)
            {
                Logger?.LogInformation("No grant detected, no action taken.");
                return Task.CompletedTask;
            }

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };
            
            try {
                Console.WriteLine(JsonSerializer.Serialize(grant, options));
                var badgeRecord = JsonSerializer.Deserialize<BadgeRecord>(JsonSerializer.Serialize(grant, options), options);
                Console.WriteLine(JsonSerializer.Serialize(badgeRecord));

                if (badgeRecord == null)
                {
                    Logger?.LogInformation("No badge record detected, no action taken.");
                    return Task.CompletedTask;
                }

                if (badgeRecord.Context != "https://vocalcat.com/badgefed/1.0")
                {
                    Logger?.LogInformation("Invalid context detected, no action taken.");
                    return Task.CompletedTask;
                }

                var existingLocal = _localDbService.GetGrantByNoteId(badgeRecord.NoteId);

                if (existingLocal != null)
                {
                    Logger?.LogInformation($"Grant already exists in local database: {existingLocal.Id}");
                    return Task.CompletedTask;
                }

                badgeRecord.IsExternal = true;

                Logger?.LogInformation($"Creating new badge record in local database");

                _localDbService.CreateBadgeRecord(badgeRecord);
            }
            catch (JsonException ex)
            {
                Logger?.LogError($"Failed to deserialize badge record: {ex.Message}");
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Logger?.LogError($"An unexpected error occurred: {ex.Message}");
                return Task.CompletedTask;
            }
            
            return Task.CompletedTask;
        }
    }
}