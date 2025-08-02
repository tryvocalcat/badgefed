using System.Text.Json;
using BadgeFed.Models;
using BadgeFed.Services;
using Microsoft.Extensions.Logging;

namespace ActivityPubDotNet.Core
{
    public class ExternalBadgeService
    {
        public ILogger? Logger { get; set; }
        private readonly BadgeProcessor _badgeProcessor;

        public ExternalBadgeService(BadgeProcessor badgeProcessor)
        {
            _badgeProcessor = badgeProcessor;
        }

        public async Task<List<BadgeRecord>> ProcessExternalBadge(ActivityPubNote objectNote, LocalScopedDb db)
        {
            var records = new List<BadgeRecord>();

            Logger?.LogInformation($"Processing external badge for note in external badge service: {objectNote.Id}");

            if (objectNote!.InReplyTo != null)
            {
                Logger?.LogInformation("Reply detected, no action taken.");
                return records;
            }

            if (objectNote.Attachment == null || objectNote.Attachment.Count == 0)
            {
                Logger?.LogInformation("No attachment detected, no action taken.");
                return records;
            }

            if (objectNote.BadgeMetadata != null)
            {
                // deprecated but backward compatibility
                objectNote.Attachment.Add(objectNote.BadgeMetadata);
            }

            foreach (var grant in objectNote.Attachment)
                {
                    Logger?.LogInformation($"Processing attachment");

                    if (grant == null)
                    {
                        Logger?.LogInformation("No grant detected, no action taken.");
                        continue;
                    }

                    var options = new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        PropertyNameCaseInsensitive = true
                    };

                    try
                    {
                        var _openBadgeImportService = new OpenBadgeImportService(db);

                        // Try to deserialize as ActivityPub badge first
                        var serializedGrant = JsonSerializer.Serialize(grant, options);

                        BadgeRecord? processedBadgeRecord = null;

                        Console.WriteLine($"Serialized grant: {serializedGrant}");

                        var existingBadge = db.GetGrantByNoteId(objectNote.Id);

                        if (existingBadge != null)
                        {
                            Logger?.LogInformation($"Badge already exists: {objectNote.Id}");
                            records.Add(existingBadge);
                            return records;
                        }

                        // To be deprecated -- avoiding creating custom spec
                        if (serializedGrant.Contains("https://vocalcat.com/badgefed/1.0"))
                        {
                            Logger?.LogInformation("Processing ActivityPub badge record");
                            var badgeRecord = JsonSerializer.Deserialize<BadgeRecord>(serializedGrant, options);

                            if (badgeRecord?.Context == "https://vocalcat.com/badgefed/1.0")
                            {
                                processedBadgeRecord = await ProcessActivityPubBadge(badgeRecord, objectNote.Id, db);
                            }
                        }
                        // trying openbadges 2.0
                        else if (serializedGrant.Contains("https://w3id.org/openbadges/v2"))
                        {
                            Logger?.LogInformation("Processing OpenBadges 2.0 badge record");
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
                        else
                        {
                            records.Add(processedBadgeRecord);
                        }

                    }
                    catch (JsonException ex)
                    {
                        Logger?.LogError($"Failed to deserialize badge record: {ex.Message}");
                    }
                    catch (Exception ex)
                    {
                        Logger?.LogError($"An unexpected error occurred: {ex.Message}");
                    }
                }

            return records;
        }

        private async Task<BadgeRecord?> ProcessActivityPubBadge(BadgeRecord badgeRecord, string noteId, LocalScopedDb localDbService)
        {
            // Extract domain from noteId to get the appropriate LocalDbService
            Uri noteUri;
            if (!Uri.TryCreate(badgeRecord.NoteId, UriKind.Absolute, out noteUri))
            {
                Logger?.LogError($"Invalid note URI format: {badgeRecord.NoteId}");
                return null;
            }
            
            Console.WriteLine($"Checking if badge record already exists for note ID: {badgeRecord.NoteId}");
            var existingLocal = localDbService.GetGrantByNoteId(badgeRecord.NoteId);

            if (existingLocal != null)
            {
                Logger?.LogInformation($"Grant already exists in local database: {existingLocal.Id}");
                Console.WriteLine($"Grant already exists in local database: {existingLocal.Id}");
                return existingLocal;
            }

            Logger?.LogInformation($"Grant is new, recording it in local database");
            Console.WriteLine($"Grant is new, recording it in local database");

            badgeRecord.IsExternal = true;
            localDbService.CreateBadgeRecord(badgeRecord);

            return badgeRecord;
        }
    }
}