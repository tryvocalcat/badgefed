using ActivityPubDotNet.Core;
using BadgeFed.Services;
using Microsoft.AspNetCore.Mvc;

namespace BadgeFed.Controllers
{
    [ApiController]
    [Route("admin/grant")]
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
            
            var records = _localDbService.GetBadgeRecords(recordId);

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
            
            
            Console.WriteLine("Badge found");

            var json = System.IO.File.ReadAllText(filePath);

            var note = System.Text.Json.JsonSerializer.Deserialize<ActivityPubDotNet.Core.ActivityPubNote>(json);

            var createNote = NotesService.GetCreateNote(note!, actor);

            var followers = _localDbService.GetFollowersByActorId(actor.Id);

            var actorHelper = new ActorHelper(actor.PrivateKeyPemClean!, actor.KeyId);
            
            // Actor is the account who wants to follow
            var serializedNote = System.Text.Json.JsonSerializer.Serialize(createNote);    

            Console.WriteLine($"Serialized note: {serializedNote}");	
            Console.WriteLine($"Followers: {followers.Count}");

            foreach(var follower in followers) {
                try {
                    var fediverseInfo = await actorHelper.FetchActorInformationAsync(follower.FollowerUri);

                    await actorHelper.SendPostSignedRequest(serializedNote, new Uri(fediverseInfo.Inbox));

                    Console.WriteLine($"Sent note to {follower.FollowerUri}");
                } catch (Exception e) {
                    Console.WriteLine($"Failed to send note to {follower.FollowerUri}");
                    Console.WriteLine(e.Message);
                }
            }

            return Redirect("/admin/grants");
        }

        [HttpGet("{id}/notify")]
        public async Task<IActionResult> NotifyAcceptLink(string id)
        {
            var recordId = long.Parse(id);
            // - retrieve badges without fingerprint, no acceptkey, but acceptedOn
            var records = _localDbService.GetBadgeRecords(recordId);
            
            if (records.Count == 0)
            {
                return NotFound("No badges to notify");
            }

            var record = records.FirstOrDefault()!;

            var badge = _localDbService.GetBadgeDefinitionById(record.Badge.Id);

            var actor = _localDbService.GetActorById(badge.IssuedBy);

            record.Badge = badge;
            record.Actor = actor;

            var note = NotesService.GetPrivateBadgeNotificationNote(record);

            var createAction = NotesService.GetCreateNote(note, actor);

            var serializedPayload = System.Text.Json.JsonSerializer.Serialize(createAction);

            var actorHelper = new ActorHelper(actor.PrivateKeyPemClean!, actor.KeyId);
           
            try {
                var fediverseInfo = await actorHelper.FetchActorInformationAsync(record.IssuedToSubjectUri);

                await actorHelper.SendPostSignedRequest(serializedPayload, new Uri(fediverseInfo.Inbox));

                Console.WriteLine($"Sent note to {record.IssuedToSubjectUri}");
            } catch (Exception e) {
                Console.WriteLine($"Failed to send note to {record.IssuedToSubjectUri}");
                Console.WriteLine(e.Message);
            }

            return Redirect("/admin/grants");
        }

        /** Process signs and create a badgenote **/
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

                // - generate activitypub note
                var note = BadgeService.GetNoteFromBadgeRecord(record);

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

            // Redirect to the grants administration page after processing
            return Redirect("/admin/grants");
        }
    }
}