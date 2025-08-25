using System.Text.Json;
using BadgeFed.Models;
using Microsoft.Extensions.Logging;

namespace BadgeFed.Services
{
    public class OpenBadgeImportService
    {
        private readonly LocalDbService _localDbService;
        public ILogger? Logger { get; set; }

        public OpenBadgeImportService(LocalDbService localDbService)
        {
            _localDbService = localDbService;
        }

        public async Task<BadgeRecord?> ImportOpenBadge(string openBadgeJson)
        {
            Logger?.LogInformation("Processing OpenBadge import");

            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                Console.WriteLine($"OpenBadge JSON: {openBadgeJson}");
                // Parse the OpenBadge JSON
                var openBadge = JsonSerializer.Deserialize<OpenBadgeAssertion>(openBadgeJson, options);

                if (openBadge == null)
                {
                    Logger?.LogInformation("Invalid OpenBadge format");
                    return null;
                }

                var issuerUrl =  openBadge.Badge.Issuer.Url ?? string.Empty;


                // extracting noteId from openBadge.Id is the last part of the url
                var badgeId = openBadge.Id.Split('/').Last();
                
                Console.WriteLine($"Checking if badge record already exists for note ID: {openBadge.Id} / badgeId: {badgeId}");

                var existingLocal = _localDbService.GetGrantByNoteId(badgeId);

                if (existingLocal != null)
                {
                    Logger?.LogInformation($"Grant already exists in local database: {existingLocal.Id}");
                    Console.WriteLine($"Grant already exists in local database: {existingLocal.Id}");

                    return existingLocal;
                }

                // Convert OpenBadge to BadgeRecord
                var badgeRecord = new BadgeRecord
                {
                    Context = "https://w3id.org/openbadges/v2",
                    Type = "BadgeRecord",
                    Title = openBadge.Badge.Name,
                    Description = openBadge.Badge.Description,
                    IssuedBy = issuerUrl,
                    Image = openBadge.Badge.Image,
                    EarningCriteria = openBadge.Badge.Criteria?.Narrative ?? "",
                    IssuedOn = DateTime.Parse(openBadge.IssuedOn),
                    IssuedToSubjectUri = openBadge.Recipient.Identity,
                    IssuedToName = openBadge.Recipient.Identity,
                    NoteId = openBadge.Id,
                    IsExternal = true,
                    Visibility = "Public",
                    AcceptedOn = DateTime.UtcNow // Since we're importing it, we consider it accepted
                };

                // Create the badge record
                _localDbService.CreateBadgeRecord(badgeRecord);
                Logger?.LogInformation($"Successfully imported OpenBadge: {openBadge.Id}");

                return badgeRecord;
            }
            catch (JsonException ex)
            {
                Logger?.LogError($"Failed to parse OpenBadge JSON: {ex.Message}");
                Console.WriteLine($"Error parsing OpenBadge JSON: {ex}");
                return null;
            }
            catch (Exception ex)
            {
                Logger?.LogError($"Error importing OpenBadge: {ex.Message}");
                Console.WriteLine($"Unexpected error importing OpenBadge: {ex}");
                return null;
            }
        }
    }

    // Helper classes to deserialize OpenBadge JSON
    public class OpenBadgeAssertion
    {
        public string Type { get; set; } = "Assertion";
        public string Id { get; set; } = "";
        public OpenBadgeRecipient Recipient { get; set; } = new();
        public OpenBadgeClass Badge { get; set; } = new();
        public string IssuedOn { get; set; } = "";
        public OpenBadgeVerification? Verification { get; set; }
        public OpenBadgeEvidence[]? Evidence { get; set; }
    }

    public class OpenBadgeRecipient
    {
        public string Type { get; set; } = "";
        public string Identity { get; set; } = "";
        public bool Hashed { get; set; }
    }

    public class OpenBadgeClass
    {
        public string Type { get; set; } = "BadgeClass";
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Image { get; set; } = "";
        public OpenBadgeCriteria? Criteria { get; set; }
        public OpenBadgeIssuer Issuer { get; set; } = new();
    }

    public class OpenBadgeIssuer
    {
        public string? Type { get; set; }
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Url { get; set; }
        public string? Email { get; set; }
    }

    public class OpenBadgeCriteria
    {
        public string? Narrative { get; set; }
    }

    public class OpenBadgeVerification
    {
        public string Type { get; set; } = "";
    }

    public class OpenBadgeEvidence
    {
        public string Type { get; set; } = "";
        public string Id { get; set; } = "";
        public string? Narrative { get; set; }
    }
}