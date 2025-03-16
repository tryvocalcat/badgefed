using ActivityPubDotNet.Core;
using BadgeFed.Services;
using Microsoft.AspNetCore.Mvc;

namespace BadgeFed.Controllers
{
    [ApiController]
    [Route("admin/record")]
    public class AdminBadgeController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly LocalDbService _localDbService;

        public AdminBadgeController(IConfiguration configuration, LocalDbService localDbService)
        {
            _configuration = configuration;
            _localDbService = localDbService;
        }

        [HttpGet("{id}/broadcast")]
        public async Task<IActionResult> BroadcastBadge(string id)
        {
            var recordId = long.Parse(id);
            
            var records = _localDbService.GetBadgeRecords(null, recordId);

            var record = records.FirstOrDefault()!;

            var badge = _localDbService.GetBadgeDefinitionById(record.Badge.Id);

            var actor = _localDbService.GetActorById(badge.IssuedBy);

            record.Badge = badge;
            record.Actor = actor;
            
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

        [HttpGet("{id}/process")]
        public async Task<IActionResult> ProcessBadge(string id)
        {
            var recordId = long.Parse(id);
            // - retrieve badges without fingerprint, no acceptkey, but acceptedOn
            var records = _localDbService.GetBadgeRecordsToProcess(recordId);
            
            if (records.Count == 0)
            {
                return NotFound("No badges to process");
            }

            foreach (var record in records)
            {
                var badge = _localDbService.GetBadgeDefinitionById(record.Badge.Id);

                var actor = _localDbService.GetActorById(badge.IssuedBy);

                record.Badge = badge;
                record.Actor = actor;

                var recipient = _localDbService.GetRecipientByIssuedTo(record.IssuedTo);

                // - generate activitypub note
                var note = BadgeService.GetNoteFromBadgeRecord(record, recipient);

                var badgeService = new BadgeService(_localDbService);

                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "badges", $"{id}.json");

                var serializedNote = System.Text.Json.JsonSerializer.Serialize(note);

                // save note
                await System.IO.File.WriteAllTextAsync(filePath, serializedNote);

                 // - generate/update fingerprint
                record.FingerPrint = badgeService.GetFingerprint(note, record);

                // - update record
                _localDbService.UpdateBadgeSignature(record);
            }
            
            return Ok();
        }
    }
}