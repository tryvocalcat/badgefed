using BadgeFed.Models;
using BadgeFed.Services;
using Microsoft.AspNetCore.Mvc;

namespace BadgeFed.Controllers
{
    [ApiController]
    [Route("grant")]
    public class BadgeController : ControllerBase
    {
        private LocalScopedDb _localDbService;
        
        private BadgeService _badgeService { get; }

        public BadgeController(LocalScopedDb localDbService, BadgeService badgeService)
        {
            _localDbService = localDbService;
            _badgeService = badgeService;
        }

        [HttpGet("{noteId}")]
        public IActionResult GetBadge(string noteId)
        {
            var accept = Request.Headers["Accept"].ToString();

            if (!BadgeFed.Core.ActivityPubHelper.IsActivityPubRequest(accept))
            {
                return Redirect($"/view/grant/{noteId}");
            }

            var record = _localDbService.GetGrantByNoteId(noteId);

            if (record.IsExternal)
            {
                // TODO: avoid loop defensive if it is the same as current url
                return Redirect(record.NoteId);
            }

            var actor = _localDbService.GetActorByUri(record.IssuedBy);

            record.Actor = actor;
            
            var note = _badgeService.GetNoteFromBadgeRecord(record);

            var options = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            };

            var json = System.Text.Json.JsonSerializer.Serialize(note, options);
            
            return Content(json, "application/activity+json");
        }
    }
}