using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using BadgeFed.Services;
using BadgeFed.Models;

namespace BadgeFed.Controllers
{
    [ApiController]
    [Route("openbadge")]
    public class OpenBadgeController : ControllerBase
    {
        private readonly LocalDbService _localDbService;

        public OpenBadgeController(LocalDbService localDbService)
        {
            _localDbService = localDbService;
        }

        [HttpGet("issuer/{domain}/{username}")]
        public IActionResult GetIssuer(string domain, string username)
        {
            var actor = _localDbService.GetActorByFilter($"Username = \"{username}\" AND Domain = \"{domain}\"");
            
            if (actor == null)
            {
                return NotFound("Issuer not found");
            }

            var issuer = new
            {
                _context = "https://w3id.org/openbadges/v2",
                type = "Profile",
                id = $"https://{actor.Domain}/openbadge/issuer/{actor.Domain}/{actor.Username}",
                name = actor.FullName,
                url = actor.InformationUri,
                email = $"{actor.Username}@{actor.Domain}"
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var json = JsonSerializer.Serialize(issuer, options);
            json = json.Replace("\"_context\":", "\"@context\":");

            return Content(json, "application/json");
        }

        [HttpGet("class/{id}")]
        public IActionResult GetBadgeClass(long id)
        {
            var badge = _localDbService.GetBadgeDefinitionById(id);
            
            if (badge == null)
            {
                return NotFound("Badge not found");
            }

            var actor = _localDbService.GetActorById(badge.IssuedBy);

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
                issuer = $"https://{actor.Domain}/openbadge/issuer/{actor.Domain}/{actor.Username}"
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var json = JsonSerializer.Serialize(badgeClass, options);
            json = json.Replace("\"_context\":", "\"@context\":");

            return Content(json, "application/json");
        }

        [HttpGet("{noteId}")]
        public IActionResult GetOpenBadge(string noteId)
        {
            var record = _localDbService.GetGrantByNoteId(noteId);
            
            if (record == null)
            {
                return NotFound("Badge not found");
            }

            var badge = _localDbService.GetBadgeDefinitionById(record.Badge.Id);
            var actor = _localDbService.GetActorByFilter($"Uri = \"{record.IssuedBy}\"")!;

            record.Badge = badge;
            record.Actor = actor;

            var openBadge = new
            {
                _context = "https://w3id.org/openbadges/v2",
                type = "Assertion",
                id = $"https://{record.Actor.Domain}/openbadge/{noteId}",
                recipient = new
                {
                    type = "url",
                    identity = record.IssuedToSubjectUri,
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
                    issuer = $"https://{actor.Domain}/openbadge/issuer/{actor.Domain}/{actor.Username}"
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
                        id = $"https://{record.Actor.Domain}/view/grant/{noteId}",
                        narrative = "This badge was issued through BadgeFed, a decentralized badge issuing platform using ActivityPub."
                    }
                }
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var json = JsonSerializer.Serialize(openBadge, options);
            json = json.Replace("\"_context\":", "\"@context\":");

            return Content(json, "application/json");
        }
    }
} 