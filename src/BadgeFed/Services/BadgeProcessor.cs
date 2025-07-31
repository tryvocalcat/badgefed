using BadgeFed.Models;
using ActivityPubDotNet.Core;
using System.Text.Json;

namespace BadgeFed.Services;

public class BadgeProcessor
{
    private readonly LocalScopedDb _localDbService;
    private readonly BadgeService _badgeService;

    public BadgeProcessor(LocalScopedDb localDbService, BadgeService badgeService)
    {
        _localDbService = localDbService;
        _badgeService = badgeService;
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

    public async Task ProcessFollowerAsync(Follower follower)
    {
        // This method is called to process a follower
        // It can be used to send a welcome message or perform other actions
        Console.WriteLine($"Processing follower: {follower.FollowerUri}");

        if (string.IsNullOrEmpty(follower.DisplayName) || string.IsNullOrEmpty(follower.AvatarUri))
        {
            try
            {
                var actor = _localDbService.GetActorById(follower.Parent.Id);

                // Fetch actor information to get display name and avatar
                var actorHelper = new ActorHelper(actor.PrivateKeyPemClean!, actor.KeyId);
                var fediverseInfo = await actorHelper.FetchActorInformationAsync(follower.FollowerUri);

                if (fediverseInfo != null)
                {
                    follower.DisplayName = fediverseInfo.Name ?? fediverseInfo.PreferredUsername;
                    follower.AvatarUri = fediverseInfo.Icon?.Url ?? string.Empty;
                }
            } catch (Exception e)
            {
                Console.WriteLine($"Failed to fetch actor information for {follower.FollowerUri}: {e.Message}");
            }

            if (string.IsNullOrEmpty(follower.DisplayName))
            {
                var uriSegments = follower.FollowerUri.Split('/');
                follower.DisplayName = uriSegments.LastOrDefault() ?? follower.FollowerUri;
            }

            _localDbService.UpsertFollower(follower);
        }
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
        // https://{record.Actor.Domain}/view/grant/
        var noteId = BadgeService.GetNoteIdForBadgeRecord(record);

        record.NoteId = $"https://{record.Actor.Domain}/grant/{noteId}";

        // - generate activitypub note
        var note = _badgeService.GetNoteFromBadgeRecord(record);

        // - generate/update fingerprint
        record.FingerPrint = _badgeService.GetFingerprint(note, record);

        // - update record
        _localDbService.UpdateBadgeSignature(record);

        return record;
    }

    public async Task AnnounceGrantByMainActor(BadgeRecord record)
    {
        try
        {
            // Find the main actor (typically the server's default actor)
            var mainActor = _localDbService.GetMainActor();

            if (mainActor == null)
            {
                Console.WriteLine("Main actor not found, cannot boost badge grant");
            }

            // The original note ID that we want to boost
            var originalNoteId = record.NoteId;

            if (string.IsNullOrEmpty(originalNoteId))
            {
                Console.WriteLine("Note ID is missing, cannot boost badge grant");
            }

            // Create the Announce activity
            var announceId = $"{mainActor.Uri}/announce/{Guid.NewGuid()}";

            // Create the Announce object
            var announceActivity = new Dictionary<string, object>
            {
                ["@context"] = "https://www.w3.org/ns/activitystreams",
                ["id"] = announceId,
                ["type"] = "Announce",
                ["actor"] = mainActor.Uri.ToString(),
                ["published"] = DateTime.UtcNow.ToString("o"),
                ["to"] = new List<string> { "https://www.w3.org/ns/activitystreams#Public" },
                ["cc"] = new List<string> { $"{mainActor.Uri}/followers" },
                ["object"] = originalNoteId
            };

            var actorHelper = new ActorHelper(mainActor.PrivateKeyPemClean!, mainActor.KeyId);

            // Serialize the announcement
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var serializedAnnouncement = JsonSerializer.Serialize(announceActivity, options);

            Console.WriteLine($"Boosting badge grant: {originalNoteId}");

            await NotifyFollowersOfNote(serializedAnnouncement, mainActor);
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to boost badge grant: {e.Message}");
        }
    }

    private async Task NotifyFollowersOfNote(string serializedActivityPubObject, Actor actor)
    {
        var followers = _localDbService.GetFollowersByActorId(actor.Id);

        var actorHelper = new ActorHelper(actor.PrivateKeyPemClean!, actor.KeyId);

        Console.WriteLine($"Serialized note: {serializedActivityPubObject}");
        Console.WriteLine($"Followers: {followers.Count}");

        var endpointsAlreadySent = new List<string>();

        foreach (var follower in followers)
        {
            try
            {
                var fediverseInfo = await actorHelper.FetchActorInformationAsync(follower.FollowerUri);

                Console.WriteLine($"Follower: {follower.FollowerUri}");
                Console.WriteLine($"Inbox: {System.Text.Json.JsonSerializer.Serialize(fediverseInfo)}");

                var endpointUri = fediverseInfo.Endpoints?.SharedInbox ?? fediverseInfo.Inbox;

                if (endpointsAlreadySent.Contains(endpointUri))
                {
                    Console.WriteLine($"Skipping {endpointUri}");
                    continue;
                }

                endpointsAlreadySent.Add(endpointUri);

                await actorHelper.SendPostSignedRequest(serializedActivityPubObject, new Uri(fediverseInfo.Inbox));

                Console.WriteLine($"Sent note to {follower.FollowerUri}");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to send note to {follower.FollowerUri}");
                Console.WriteLine(e.Message);
            }
        }
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

        Console.WriteLine("Badge found");

        var note = _badgeService.GetNoteFromBadgeRecord(record);

        var createNote = NotesService.GetCreateNote(note!, actor);

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        var serializedNote = JsonSerializer.Serialize(createNote, options);

        await NotifyFollowersOfNote(serializedNote, actor);

        await AnnounceGrantByMainActor(record);

        return record;
    }
}