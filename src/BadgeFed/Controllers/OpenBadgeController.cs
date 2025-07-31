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

        public OpenBadgeController(LocalScopedDb localDbService, OpenBadgeService openBadgeService)
        {
            _localDbService = localDbService;
            _openBadgeService = openBadgeService;
        }

        [HttpGet("issuer/{domain}/{username}")]
        public IActionResult GetIssuer(string domain, string username)
        {
            var actor = _localDbService.GetActorByFilter($"Username = \"{username}\" AND Domain = \"{domain}\"");
            
            if (actor == null)
            {
                return NotFound("Issuer not found");
            }

            var json = _openBadgeService.GetIssuerJson(actor);
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

            var json = _openBadgeService.GetBadgeClassJson(badge, actor);
            return Content(json, "application/json");
        }

        [HttpGet("{noteId}")]
        public IActionResult GetOpenBadge(string noteId)
        {
            var record = _openBadgeService.GetOpenBadgeFromBadgeRecord(noteId);
            
            if (record == null)
            {
                return NotFound("Badge not found");
            }

            var json = _openBadgeService.GetOpenBadgeJson(record);
            return Content(json, "application/json");
        }
    }
} 