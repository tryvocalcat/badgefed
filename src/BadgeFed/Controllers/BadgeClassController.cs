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
        private readonly LocalScopedDb _localDbService;
        private readonly ILogger<BadgeClassController> _logger;

        public BadgeClassController(LocalScopedDb localDbService, ILogger<BadgeClassController> logger)
        {
            _localDbService = localDbService;
            _logger = logger;
        }

        [HttpGet("{id}")]
        public IActionResult GetBadgeClass(long id)
        {
            _logger.LogInformation("[{RequestHost}] Fetching badge class for badge ID: {BadgeId}", Request.Host, id);
            
            var badge = _localDbService.GetBadgeDefinitionById(id);
            
            if (badge == null)
            {
                _logger.LogWarning("[{RequestHost}] Badge not found for ID: {BadgeId}", Request.Host, id);
                return NotFound("Badge not found");
            }
            
            _logger.LogInformation("[{RequestHost}] Successfully retrieved badge class for ID: {BadgeId}", Request.Host, id);

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