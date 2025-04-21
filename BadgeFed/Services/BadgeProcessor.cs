using BadgeFed.Models;
using ActivityPubDotNet.Core;

namespace BadgeFed.Services;

public class BadgeProcessor
{
    private readonly LocalDbService _localDbService;

    public BadgeProcessor(LocalDbService localDbService)
    {
        _localDbService = localDbService;
    }

    private BadgeRecord? GetBadgeRecord(long recordId)
    {
        // - retrieve badges without fingerprint, no acceptkey, but acceptedOn
        var records = _localDbService.GetBadgeRecords(recordId);
        
        if (records.Count == 0)
        {
            return null;
        }

        var record = records.FirstOrDefault()!;

        var badge = _localDbService.GetBadgeDefinitionById(record.Badge.Id);

        var actor = _localDbService.GetActorById(badge.IssuedBy);

        record.Badge = badge;
        record.Actor = actor;

        return record;
    }

    public async Task<BadgeRecord?> NotifyGrantAcceptLink(long recordId)
    {
        var record = GetBadgeRecord(recordId);

        if (record == null)
        {
            return null;
        }

        var note = NotesService.GetPrivateBadgeNotificationNote(record);

        try {
            await NotifyNote(note, record);
        } catch (Exception e) {
            Console.WriteLine($"Failed to send note to {record.IssuedToSubjectUri}");
            Console.WriteLine(e.Message);
        }
        
         _localDbService.NotifyGrant(record.Id);

        return record;
    }

    public async Task<BadgeRecord?> NotifyProcessedGrant(long recordId)
    {
        var record = GetBadgeRecord(recordId);

        if (record == null)
        {
            return null;
        }

         var note = NotesService.GetPrivateBadgeProcessedNote(record);

        try {
            await NotifyNote(note, record);
        } catch (Exception e) {
            Console.WriteLine($"Failed to send note to {record.IssuedToSubjectUri}");
            Console.WriteLine(e.Message);
        }

        return record;
    }

    public async Task NotifyNote(ActivityPubNote note, BadgeRecord record)
    {
        var actor = record.Actor;

        var actorHelper = new ActorHelper(actor.PrivateKeyPemClean!, actor.KeyId);

        var fediverseInfo = await actorHelper.FetchActorInformationAsync(record.IssuedToSubjectUri);
    
        record.IssuedToName = !string.IsNullOrEmpty(record.IssuedToName) ? record.IssuedToName : fediverseInfo.Name;

        var createAction = NotesService.GetCreateNote(note, actor);

        var serializedPayload = System.Text.Json.JsonSerializer.Serialize(createAction);

        await actorHelper.SendPostSignedRequest(serializedPayload, new Uri(fediverseInfo.Inbox));

        Console.WriteLine($"Sent note to {record.IssuedToSubjectUri}");
    }

    public async Task<BadgeRecord?> SignAndGenerateBadge(long recordId)
    {
        // - retrieve badges without fingerprint, no acceptkey, but acceptedOn
        var records = _localDbService.GetBadgeRecordsToProcess(recordId);
        
        if (records.Count == 0)
        {
            return null;
        }

        var record = records.FirstOrDefault()!;
       
        var badge = _localDbService.GetBadgeDefinitionById(record.Badge.Id);

        var actor = _localDbService.GetActorById(badge.IssuedBy);

        record.Badge = badge;
        record.Actor = actor;
        record.NoteId = BadgeService.GetNoteIdForBadgeRecord(record);

        // - generate activitypub note
        var note = BadgeService.GetNoteFromBadgeRecord(record);

        var badgeService = new BadgeService(_localDbService);

        var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "badges", $"{record.NoteId}.json");

        var serializedNote = System.Text.Json.JsonSerializer.Serialize(note);

        // save note
        await System.IO.File.WriteAllTextAsync(filePath, serializedNote);

        // - generate/update fingerprint
        record.FingerPrint = badgeService.GetFingerprint(note, record);

        // - update record
        _localDbService.UpdateBadgeSignature(record);

        return record;
    }

    public async Task<BadgeRecord?> BroadcastGrant(long recordId)
    {
        var records = _localDbService.GetBadgeRecords(recordId);

        if (records.Count == 0)
        {
            return null;
        }

        var record = records.FirstOrDefault();

        var badge = _localDbService.GetBadgeDefinitionById(record.Badge.Id);

        var actor = _localDbService.GetActorById(badge.IssuedBy);

        record.Badge = badge;
        record.Actor = actor;
        
        var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "badges", $"{record.NoteId}.json");
        
        if (!System.IO.File.Exists(filePath))
        {
            return null;
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

        return record;
    }
}