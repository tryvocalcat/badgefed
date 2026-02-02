using BadgeFed.Models;
using BadgeFed.Services;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;

namespace BadgeFed.Controllers
{
    [ApiController]
    [Route("grant")]
    public class BadgeController : ControllerBase
    {
        private LocalScopedDb _localDbService;
        private BadgeService _badgeService { get; }
        private readonly ILogger<BadgeController> _logger;

        private static ConcurrentDictionary<string, string> _notesInMemoryCache = new ConcurrentDictionary<string, string>();

        public BadgeController(LocalScopedDb localDbService, BadgeService badgeService, ILogger<BadgeController> logger)
        {
            _localDbService = localDbService;
            _badgeService = badgeService;
            _logger = logger;
        }

        [HttpGet("{noteId}")]
        public IActionResult GetBadge(string noteId)
        {
            var userAgent = Request.Headers["User-Agent"].ToString();
            var referer = Request.Headers["Referer"].ToString();
            
            _logger.LogInformation("[{RequestHost}] Fetching badge grant for noteId: {NoteId} | User-Agent: {UserAgent} | Referer: {Referer}", 
                Request.Host, noteId, userAgent, referer);
            
            var accept = Request.Headers["Accept"].ToString();

            if (!BadgeFed.Core.ActivityPubHelper.IsActivityPubRequest(accept))
            {
                _logger.LogInformation("[{RequestHost}] Non-ActivityPub request for badge {NoteId}, redirecting to view", Request.Host, noteId);
                return Redirect($"/view/grant/{noteId}");
            }

            if (_notesInMemoryCache.ContainsKey(noteId))
            {
                _logger.LogInformation("[{RequestHost}] Serving badge grant {NoteId} from in-memory cache", Request.Host, noteId);
                
                // Add caching headers - cache for 24 hours (86400 seconds)
                Response.Headers.Add("Cache-Control", "public, max-age=86400");
                Response.Headers.Add("Expires", DateTime.UtcNow.AddHours(24).ToString("R"));
                
                return Content(_notesInMemoryCache[noteId], "application/activity+json");
            }

            var record = _localDbService.GetGrantByNoteId(noteId);

            if (record.IsExternal)
            {
                _logger.LogInformation("[{RequestHost}] Badge {NoteId} is external, redirecting to {ExternalUrl}", Request.Host, noteId, record.NoteId);
                // TODO: avoid loop defensive if it is the same as current url
                return Redirect(record.NoteId);
            }
            
            _logger.LogInformation("[{RequestHost}] Successfully retrieved badge grant {NoteId}", Request.Host, noteId);

            var actor = _localDbService.GetActorByUri(record.IssuedBy);

            record.Actor = actor;
            
            var note = _badgeService.GetNoteFromBadgeRecord(record);

            var options = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            };

            var json = System.Text.Json.JsonSerializer.Serialize(note, options);

            _notesInMemoryCache[noteId] = json;
            
            // Add caching headers - cache for 24 hours (86400 seconds)
            Response.Headers.Add("Cache-Control", "public, max-age=86400");
            Response.Headers.Add("Expires", DateTime.UtcNow.AddHours(24).ToString("R"));
            
            return Content(json, "application/activity+json");
        }
    }
}