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
        private readonly LocalScopedDb _localDbService;
        private readonly OpenBadgeService _openBadgeService;
        private readonly ILogger<OpenBadgeController> _logger;

        public OpenBadgeController(LocalScopedDb localDbService, OpenBadgeService openBadgeService, ILogger<OpenBadgeController> logger)
        {
            _localDbService = localDbService;
            _openBadgeService = openBadgeService;
            _logger = logger;
        }

        [HttpGet("issuer/{domain}/{username}")]
        public IActionResult GetIssuer(string domain, string username)
        {
            _logger.LogInformation("[{RequestHost}] Fetching OpenBadge issuer for {Username}@{Domain}", Request.Host, username, domain);
            
            var actor = _localDbService.GetActorByFilter($"Username = \"{username}\" AND Domain = \"{domain}\"");
            
            if (actor == null)
            {
                _logger.LogWarning("[{RequestHost}] Issuer not found: {Username}@{Domain}", Request.Host, username, domain);
                return NotFound("Issuer not found");
            }
            
            _logger.LogInformation("[{RequestHost}] Successfully retrieved issuer: {Username}@{Domain}", Request.Host, username, domain);

            var json = _openBadgeService.GetIssuerJson(actor);
            return Content(json, "application/json");
        }

        [HttpGet("class/{id}")]
        public IActionResult GetBadgeClass(long id)
        {
            _logger.LogInformation("[{RequestHost}] Fetching OpenBadge class for badge ID: {BadgeId}", Request.Host, id);
            
            var badge = _localDbService.GetBadgeDefinitionById(id);
            
            if (badge == null)
            {
                _logger.LogWarning("[{RequestHost}] Badge not found for OpenBadge class request: {BadgeId}", Request.Host, id);
                return NotFound("Badge not found");
            }
            
            _logger.LogInformation("[{RequestHost}] Successfully retrieved OpenBadge class for badge ID: {BadgeId}", Request.Host, id);

            var actor = _localDbService.GetActorById(badge.IssuedBy);

            var json = _openBadgeService.GetBadgeClassJson(badge, actor);
            return Content(json, "application/json");
        }

        [HttpGet("{noteId}")]
        public IActionResult GetOpenBadge(string noteId)
        {
            _logger.LogInformation("[{RequestHost}] Fetching OpenBadge for noteId: {NoteId}", Request.Host, noteId);
            
            var record = _openBadgeService.GetOpenBadgeFromBadgeRecord(noteId);
            
            if (record == null)
            {
                _logger.LogWarning("[{RequestHost}] Badge record not found for OpenBadge request: {NoteId}", Request.Host, noteId);
                return NotFound("Badge not found");
            }
            
            _logger.LogInformation("[{RequestHost}] Successfully retrieved OpenBadge for noteId: {NoteId}", Request.Host, noteId);

            var json = _openBadgeService.GetOpenBadgeJson(record);
            return Content(json, "application/json");
        }
    }
} 