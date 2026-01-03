using BadgeFed.Models;
using ActivityPubDotNet.Core;
using System.Text.Json;

namespace BadgeFed.Services;

public class BadgeProcessor
{
    private readonly LocalScopedDb _localDbService;
    private readonly BadgeService _badgeService;
    private readonly ILogger<BadgeProcessor>? _logger;

    public BadgeProcessor(LocalScopedDb localDbService, BadgeService badgeService, ILogger<BadgeProcessor>? logger = null)
    {
        _localDbService = localDbService;
        _badgeService = badgeService;
        _logger = logger;
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
        _logger?.LogInformation("NotifyGrantAcceptLink for record ID: {RecordId}", recordId);

        var record = GetBadgeRecord(recordId);

        if (record == null)
        {
            return null;
        }

        var note = NotesService.GetPrivateBadgeNotificationNote(record);

        try {
            await NotifyNote(note, record);
        } catch (Exception e) {
            Console.WriteLine($"ERROR Failed to send note to {record.IssuedToSubjectUri} - {e.Message}");
        }
        
         _localDbService.NotifyGrant(record.Id);

        return record;
    }

    public async Task<BadgeRecord?> NotifyProcessedGrant(long recordId)
    {
        _logger?.LogInformation("NotifyProcessedGrant for record ID: {RecordId}", recordId);

        var record = GetBadgeRecord(recordId);

        if (record == null)
        {
            return null;
        }

         var note = NotesService.GetPrivateBadgeProcessedNote(record);

        try {
            await NotifyNote(note, record);
        } catch (Exception e) {
            Console.WriteLine($"ERROR Failed to send note to {record.IssuedToSubjectUri} - {e.Message}");
        }

        return record;
    }

    public async Task ProcessFollowerAsync(Follower follower)
    {
        // This method is called to process a follower
        // It can be used to send a welcome message or perform other actions
        _logger?.LogInformation("Processing follower: {FollowerUri}", follower.FollowerUri);

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
                _logger?.LogError(e, "Failed to fetch actor information for {FollowerUri}", follower.FollowerUri);
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

        _logger?.LogInformation("Sent note to {IssuedToSubjectUri}", record.IssuedToSubjectUri);
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
       
        _logger?.LogInformation("Processing badge record: {BadgeRecord}", System.Text.Json.JsonSerializer.Serialize(record));

        var badge = _localDbService.GetBadgeDefinitionById(record.Badge.Id);

        _logger?.LogInformation("Badge definition: {BadgeDefinition}", System.Text.Json.JsonSerializer.Serialize(badge));

        if (badge == null)
        {
            _logger?.LogWarning("Badge definition not found for ID: {BadgeId}", record.Badge.Id);
            return null;
        }


        _logger?.LogInformation("Badge definition found for ID: {BadgeId}", record.Badge.Id);

        var actor = _localDbService.GetActorById(badge.IssuedBy);

        _logger?.LogInformation("Actor found for ID: {ActorId}", badge.IssuedBy);

        record.Badge = badge;
        record.Actor = actor;
        // https://{record.Actor.Domain}/view/grant/
        var noteId = BadgeService.GetNoteIdForBadgeRecord(record);

        _logger?.LogInformation("Generated note ID: {NoteId}", noteId);

        record.NoteId = $"https://{record.Actor.Domain}/grant/{noteId}";

        _logger?.LogInformation("Full note URL: {NoteUrl}", record.NoteId);
        
        // - generate activitypub note
        var note = _badgeService.GetNoteFromBadgeRecord(record);

        _logger?.LogInformation("Generated ActivityPub note: {ActivityPubNote}", System.Text.Json.JsonSerializer.Serialize(note));

        // - generate/update fingerprint
        record.FingerPrint = _badgeService.GetFingerprint(note, record);

        _logger?.LogInformation("Generated fingerprint: {FingerPrint}", record.FingerPrint);

        // - update record
        _localDbService.UpdateBadgeSignature(record);

        _logger?.LogInformation("Updated badge record with signature for ID: {RecordId}", record.Id);

        return record;
    }

    public async Task AnnounceGrantByMainActor(BadgeRecord record)
    {
        _logger?.LogInformation("Checking if record is boosted - Record ID: {RecordId}, BoostedOn: {BoostedOn}", record.Id, record.BoostedOn);

        if (record.BoostedOn == null)
        {
           await _AnnounceGrantByMainActor(record);
            _localDbService.MarkBadgeRecordAsBoosted(record.Id);
        }
    }

    public async Task RevokeGrant(BadgeRecord record)
    {
        _logger?.LogInformation("Revoking grant for badge record ID: {RecordId}", record.Id);

        try
        {
            // Get the actor who issued the badge
            var badge = _localDbService.GetBadgeDefinitionById(record.Badge.Id);
            var actor = _localDbService.GetActorById(badge.IssuedBy);

            if (actor == null)
            {
                _logger?.LogWarning("Actor not found, cannot revoke badge grant for record ID: {RecordId}", record.Id);
                return;
            }

            // The note ID that we want to delete/revoke
            var noteId = record.NoteId;

            if (string.IsNullOrEmpty(noteId))
            {
                _logger?.LogWarning("Note ID is missing, cannot revoke badge grant for record ID: {RecordId}", record.Id);
                return;
            }

            // Create the Delete activity
            var deleteId = $"{actor.Uri}/delete/{Guid.NewGuid()}";

            // Create the Delete object following ActivityPub spec
            var deleteActivity = new Dictionary<string, object>
            {
                ["@context"] = "https://www.w3.org/ns/activitystreams",
                ["id"] = deleteId,
                ["type"] = "Delete",
                ["actor"] = actor.Uri.ToString(),
                ["published"] = DateTime.UtcNow.ToString("o"),
                ["to"] = new List<string> { "https://www.w3.org/ns/activitystreams#Public" },
                ["cc"] = new List<string> { $"{actor.Uri}/followers" },
                ["object"] = noteId
            };

            // Serialize the delete activity
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var serializedDelete = JsonSerializer.Serialize(deleteActivity, options);

            _logger?.LogInformation("Revoking badge grant: {NoteId} by {ActorUri}", noteId, actor.Uri);

            // Send the delete activity to all followers
            await NotifyFollowersOfNote(serializedDelete, actor);

            // Also notify the recipient directly if they have an inbox
            if (!string.IsNullOrEmpty(record.IssuedToSubjectUri))
            {
                try
                {
                    var actorHelper = new ActorHelper(actor.PrivateKeyPemClean!, actor.KeyId);
                    var fediverseInfo = await actorHelper.FetchActorInformationAsync(record.IssuedToSubjectUri);
                    
                    if (fediverseInfo != null && !string.IsNullOrEmpty(fediverseInfo.Inbox))
                    {
                        await actorHelper.SendPostSignedRequest(serializedDelete, new Uri(fediverseInfo.Inbox));
                        _logger?.LogInformation("Sent revocation notice to recipient: {IssuedToSubjectUri}", record.IssuedToSubjectUri);
                    }
                }
                catch (Exception e)
                {
                    _logger?.LogError(e, "Failed to notify recipient of revocation: {IssuedToSubjectUri}", record.IssuedToSubjectUri);
                }
            }

            _logger?.LogInformation("Successfully revoked badge grant: {NoteId}", noteId);
        }
        catch (Exception e)
        {
            _logger?.LogError(e, "Failed to revoke badge grant for record ID: {RecordId}", record.Id);
            throw;
        }
    }

    private async Task _AnnounceGrantByMainActor(BadgeRecord record)
    {
        try
        {
            // Find the main actor (typically the server's default actor)
            var mainActor = _localDbService.GetMainActor();

            if (mainActor == null)
            {
                _logger?.LogWarning("Main actor not found, cannot boost badge grant");
            }

            // The original note ID that we want to boost
            var originalNoteId = record.NoteId;

            if (string.IsNullOrEmpty(originalNoteId))
            {
                _logger?.LogWarning("Note ID is missing, cannot boost badge grant");
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

            _logger?.LogInformation("Boosting badge grant: {OriginalNoteId} by {MainActorUri}", originalNoteId, mainActor.Uri);

            await NotifyFollowersOfNote(serializedAnnouncement, mainActor);
        }
        catch (Exception e)
        {
            _logger?.LogError(e, "Failed to boost badge grant");
        }
    }

    private async Task NotifyFollowersOfNote(string serializedActivityPubObject, Actor actor)
    {
        _logger?.LogInformation("NotifyFollowersOfNote for actor ID: {ActorId}", actor.Id);
        var followers = _localDbService.GetFollowersByActorId(actor.Id);

        var actorHelper = new ActorHelper(actor.PrivateKeyPemClean!, actor.KeyId);

        _logger?.LogDebug("Serialized note: {SerializedNote}", serializedActivityPubObject);
        _logger?.LogInformation("Followers count: {FollowersCount}", followers.Count);

        var endpointsAlreadySent = new List<string>();

        foreach (var follower in followers)
        {
            try
            {
                var fediverseInfo = await actorHelper.FetchActorInformationAsync(follower.FollowerUri);

                _logger?.LogDebug("Processing follower: {FollowerUri}", follower.FollowerUri);
                // Console.WriteLine($"Inbox: {System.Text.Json.JsonSerializer.Serialize(fediverseInfo)}");

                var endpointUri = fediverseInfo.Endpoints?.SharedInbox ?? fediverseInfo.Inbox;

                if (endpointsAlreadySent.Contains(endpointUri))
                {
                    _logger?.LogDebug("Skipping already sent endpoint: {EndpointUri}", endpointUri);
                    continue;
                }

                endpointsAlreadySent.Add(endpointUri);

                await actorHelper.SendPostSignedRequest(serializedActivityPubObject, new Uri(fediverseInfo.Inbox));

                _logger?.LogDebug("Sent note to {FollowerUri}", follower.FollowerUri);
            }
            catch (Exception e)
            {
                _logger?.LogError(e, "Failed to send note to {FollowerUri}", follower.FollowerUri);
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

        _logger?.LogInformation("Badge found for broadcast - Record ID: {RecordId}", record.Id);

        var note = _badgeService.GetNoteFromBadgeRecord(record);

        var createNote = NotesService.GetCreateNote(note!, actor);

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        var serializedNote = JsonSerializer.Serialize(createNote, options);

        await NotifyFollowersOfNote(serializedNote, actor);

        await AnnounceGrantByMainActor(record);

        // Mark the badge record as boosted after all notifications are complete
        return record;
    }
}