using System.Text.Json;
using BadgeFed.Models;

namespace BadgeFed.Services
{
    public class OpenBadgeService
    {
        private readonly LocalScopedDb _localDbService;

        public OpenBadgeService(LocalScopedDb localDbService)
        {
            _localDbService = localDbService;
        }

        public object GetIssuerObject(Actor actor)
        {
            var issuer = new
            {
                _context = "https://w3id.org/openbadges/v2",
                type = "Profile",
                id = $"https://{actor.Domain}/openbadge/issuer/{actor.Domain}/{actor.Username}",
                name = actor.FullName,
                url = actor.Uri, // this is critical for BadgeFed compatibility
                email = $"{actor.Username}@{actor.Domain}"
            };

            return issuer;
        }

        public string GetIssuerJson(Actor actor)
        {
            var issuer = GetIssuerObject(actor);
            
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var json = JsonSerializer.Serialize(issuer, options);
            json = json.Replace("\"_context\":", "\"@context\":");

            return json;
        }

        public object GetBadgeClassObject(Badge badge, Actor actor)
        {
            var badgeClass = new
            {
                _context = "https://w3id.org/openbadges/v2",
                type = "BadgeClass",
                id = $"https://{actor.Domain}/openbadge/class/{badge.Id}",
                name = badge.Title,
                description = badge.Description,
                image = $"https://{actor.Domain}{badge.Image}",
                criteria = new
                {
                    narrative = badge.EarningCriteria
                },
                issuer = GetIssuerObject(actor)
            };

            return badgeClass;
        }

        public string GetBadgeClassJson(Badge badge, Actor actor)
        {
            var badgeClass = GetBadgeClassObject(badge, actor);
            
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var json = JsonSerializer.Serialize(badgeClass, options);
            json = json.Replace("\"_context\":", "\"@context\":");

            return json;
        }

        public object GetOpenBadgeObject(BadgeRecord record)
        {
            if (record.Badge == null || record.Badge.Id <= 0)
            {
                record.Badge = _localDbService.GetBadgeDefinitionById(record.Badge?.Id ?? 0);
            }

            if (record.Actor == null)
            {
                record.Actor = _localDbService.GetActorByFilter($"Uri = \"{record.IssuedBy}\"") ?? new Actor();
            }

            var badge = _localDbService.GetBadgeById(record.Badge.Id);
            var actor = record.Actor;
            var noteId = record.NoteId.Substring(record.NoteId.LastIndexOf('/') + 1);

            var openBadge = new
            {
                _context = "https://w3id.org/openbadges/v2",
                type = "Assertion",
                id = $"https://{actor.Domain}/openbadge/{noteId}",
                recipient = new
                {
                    type = "url",
                    identity = record.IssuedToSubjectUri, // this is critical for BadgeFed compatibility
                    hashed = false
                },
                badge = new {
                    _context = "https://w3id.org/openbadges/v2",
                    type = "BadgeClass",
                    id = $"https://{actor.Domain}/openbadge/class/{badge.Id}",
                    name = badge.Title,
                    description = badge.Description,
                    image = $"https://{actor.Domain}{badge.Image}",
                    criteria = new
                    {
                        narrative = badge.EarningCriteria
                    },
                    issuer = GetIssuerObject(actor)
                },
                verification = new
                {
                    type = "hosted"
                },
                issuedOn = record.IssuedOn.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                evidence = new[]
                {
                    new
                    {
                        type = "Evidence",
                        id = $"https://{actor.Domain}/view/grant/{noteId}",
                        narrative = "This badge was issued through BadgeFed, a decentralized badge issuing platform using ActivityPub."
                    }
                }
            };

            return openBadge;
        }

        public string GetOpenBadgeJson(BadgeRecord record)
        {
            var openBadge = GetOpenBadgeObject(record);
            
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var json = JsonSerializer.Serialize(openBadge, options);
            json = json.Replace("\"_context\":", "\"@context\":");

            return json;
        }

        public BadgeRecord? GetOpenBadgeFromBadgeRecord(string noteId)
        {
            var record = _localDbService.GetGrantByNoteId(noteId);
            
            if (record == null)
            {
                return null;
            }

            var badge = _localDbService.GetBadgeDefinitionById(record.Badge.Id);
            var actor = _localDbService.GetActorByFilter($"Uri = \"{record.IssuedBy}\"");

            if (badge != null && actor != null)
            {
                record.Badge = badge;
                record.Actor = actor;
            }

            return record;
        }
    }
}
