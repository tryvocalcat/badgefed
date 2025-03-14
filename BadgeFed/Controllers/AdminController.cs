using ActivityPubDotNet.Core;
using BadgeFed.Services;
using Microsoft.AspNetCore.Mvc;

namespace BadgeFed.Controllers
{
    [ApiController]
    [Route("admin")]
    public class AdminController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly LocalDbService _localDbService;

        public AdminController(IConfiguration configuration, LocalDbService localDbService)
        {
            _configuration = configuration;
            _localDbService = localDbService;
        }

        [HttpGet("broadcast-badge/{id}")]
        public async Task<IActionResult> BroadcastBadge(string id, [FromQuery] string account)
        {
            if (string.IsNullOrEmpty(account))
            {
                return BadRequest("Invalid actor account parameter");
            }

            var domain = account.Split('@')[1];
            var actorName = account.Split('@')[0];

            var actor = _localDbService.GetActorByFilter($"Username = \"{actorName}\" AND Domain = \"{domain}\"");

            if (actor == null)
            {
                return NotFound("Account not found on this domain");
            }

            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "badges", $"{id}.json");
                
            if (!System.IO.File.Exists(filePath))
            {
                return NotFound("Badge not found");
            }
            
            var json = System.IO.File.ReadAllText(filePath);

            var note = System.Text.Json.JsonSerializer.Deserialize<ActivityPubDotNet.Core.ActivityPubNote>(json);

            var createNote = NotesService.GetCreateNote(note!, actor);

            var followers = _localDbService.GetFollowersByActorId(actor.Id);

            var actorHelper = new ActorHelper(actor.PrivateKeyPemClean!, actor.KeyId);

            // Actor is the account who wants to follow
            var serializedNote = System.Text.Json.JsonSerializer.Serialize(createNote);    

            foreach(var follower in followers) {
                var fediverseInfo = await actorHelper.FetchActorInformationAsync(follower.FollowerUri);

                await actorHelper.SendPostSignedRequest(serializedNote, new Uri(fediverseInfo.Inbox));
            }

            return Ok(createNote);
        }

        [HttpGet("process-badge-record/{id}")]
        public async Task<IActionResult> ProcessBadge(string id)
        {
            // - retrieve badges without fingerprint, no acceptkey, but acceptedOn
            // - generate activitypub note
            // - generate/update fingerprint
            return Ok();
        }

        [HttpGet("generate-test-badge/{id}")]
        public async Task<IActionResult> GenerateBadge(string id, [FromQuery] string account)
        {
            if (string.IsNullOrEmpty(account))
            {
                return BadRequest("Invalid actor account parameter");
            }

            var domain = account.Split('@')[1];
            var actorName = account.Split('@')[0];

            var actor = _localDbService.GetActorByFilter($"Username = \"{actorName}\" AND Domain = \"{domain}\"");

            if (actor == null)
            {
                return NotFound("Account not found on this domain");
            }

            var note = NotesService.GetNote(id, 
                "This is a test badge note",
                $"https://{actor.Domain}/badge/{id}",
                actor);

            var badgePath = Path.Combine("wwwroot", "badges", $"{id}.json");
            var badgeJson = System.Text.Json.JsonSerializer.Serialize(note);

            Directory.CreateDirectory(Path.GetDirectoryName(badgePath)!);
            await System.IO.File.WriteAllTextAsync(badgePath, badgeJson);

            return Ok(note);
        }
    }
}