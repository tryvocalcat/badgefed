using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using BadgeFed.Services;
using BadgeFed.Models;

namespace BadgeFed.Controllers
{
    [ApiController]
    [Route("badge")]
    public class BadgeClassController : ControllerBase
    {
        private readonly LocalDbService _localDbService;

        public BadgeClassController(LocalDbService localDbService)
        {
            _localDbService = localDbService;
        }

        [HttpGet("{id}")]
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
                @context = "https://w3id.org/openbadges/v2",
                type = "BadgeClass",
                id = $"https://{actor.Domain}/badge/{badge.Id}",
                name = badge.Title,
                description = badge.Description,
                image = $"https://{actor.Domain}{badge.Image}",
                criteria = new
                {
                    narrative = badge.EarningCriteria
                },
                issuer = new
                {
                    @context = "https://w3id.org/openbadges/v2",
                    type = "Profile",
                    id = actor.Uri?.ToString(),
                    name = actor.FullName,
                    url = actor.InformationUri,
                    email = $"{actor.Username}@{actor.Domain}"
                }
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var json = JsonSerializer.Serialize(badgeClass, options);

            return Content(json, "application/json");
        }
    }
} 