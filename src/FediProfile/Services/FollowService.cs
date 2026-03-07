using System.Text.Json;
using System.Text.Json.Serialization;
using FediProfile.Core;
using FediProfile.Models;

namespace FediProfile.Services;

public class FollowService
{
    private readonly ILogger<FollowService> _logger;

    public FollowService(ILogger<FollowService> logger)
    {
        _logger = logger;
    }

    public async Task HandleFollowAsync(InboxMessage message, UserScopedDb db, string actorId)
    {
        if (string.IsNullOrEmpty(message.Actor))
        {
            _logger.LogWarning("Follow message missing actor");
            return;
        }

        _logger.LogInformation($"Handling Follow from {message.Actor}");
        Console.WriteLine($"$Handling Follow to {actorId} with {System.Text.Json.JsonSerializer.Serialize(message)}");

        // Fetch the follower's actor information to get their inbox
        var (publicKeyPem, privateKeyPem) = await db.GetActorKeysAsync();
        ActivityPubActor? followerActor = null;
        
        if (!string.IsNullOrEmpty(privateKeyPem))
        {
            var keyId = $"{actorId}#main-key";
            var helper = new ActorHelper(privateKeyPem, keyId, _logger);
            followerActor = await helper.FetchActorInformationAsync(message.Actor);
        }

        // Store the follower in the database with inbox URL
        try
        {
            var actorUri = new Uri(message.Actor);
            var followerDomain = actorUri.Host;
            var inbox = followerActor?.Inbox;
            
            await db.UpsertFollowerAsync(message.Actor, followerDomain, 
                avatarUri: followerActor?.Icon?.Url,
                displayName: followerActor?.Name,
                inbox: inbox);
            _logger.LogInformation($"Stored follower {message.Actor} in database");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error storing follower {message.Actor}");
        }

        // Send Accept activity back to the follower
        try
        {
            if (string.IsNullOrEmpty(privateKeyPem))
            {
                _logger.LogWarning("Cannot send Accept: missing private key");
                return;
            }

            var keyId = $"{actorId}#main-key";
            var helper = new ActorHelper(privateKeyPem, keyId, _logger);

            if (followerActor?.Inbox == null)
            {
                _logger.LogWarning($"Could not find inbox URL for follower {message.Actor}");
                return;
            }

            // Create the Accept activity
            var acceptActivity = new AcceptActivity
            {
                Context = "https://www.w3.org/ns/activitystreams",
                Actor = actorId,
                Object = new FollowObject
                {
                    Id = message.Id,
                    Actor = message.Actor,
                    Object = actorId
                },
                Id = $"{actorId}#accepts/follows/{Guid.NewGuid()}",
                To = new[] { message.Actor }
            };

            var jsonContent = JsonSerializer.Serialize(acceptActivity, new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull });

            await helper.SendPostSignedRequest(jsonContent, new Uri(followerActor.Inbox));
            _logger.LogInformation($"Sent Accept activity to {message.Actor}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error sending Accept to {message.Actor}");
        }
    }

    public async Task HandleUnfollowAsync(InboxMessage message, UserScopedDb db)
    {
        var actor = message.GetFollowActor();
        if (string.IsNullOrEmpty(actor))
        {
            _logger.LogWarning("Unfollow message missing actor");
            return;
        }

        _logger.LogInformation($"Handling Unfollow from {actor}");
        
        // Remove the follower from the database
        try
        {
            await db.RemoveFollowerAsync(actor);
            _logger.LogInformation($"Removed follower {actor} from database");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error removing follower {actor}");
        }
    }

    public async Task<(bool Success, string? Error)> SendFollowRequestAsync(string actorUrl, string inboxUrl, UserScopedDb db, string actorId)
    {
        if (string.IsNullOrEmpty(actorUrl) || string.IsNullOrEmpty(inboxUrl))
        {
            _logger.LogWarning("Cannot send follow request: missing actor or inbox URL");
            return (false, "Missing actor URL or inbox URL.");
        }

        try
        {
            var (publicKeyPem, privateKeyPem) = await db.GetActorKeysAsync();
            if (string.IsNullOrEmpty(privateKeyPem))
            {
                _logger.LogWarning("Cannot send follow request: missing private key");
                return (false, "Missing private key. Please initialize your profile keys first.");
            }

            var keyId = $"{actorId}#main-key";

            var follow = new FollowActivity
            {
                Context = "https://www.w3.org/ns/activitystreams",
                Actor = actorId,
                Object = actorUrl,
                Id = $"{actorId}/follow/{Guid.NewGuid()}"
            };

            var jsonContent = JsonSerializer.Serialize(follow, new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull });

            var helper = new ActorHelper(privateKeyPem, keyId, _logger);
            await helper.SendPostSignedRequest(jsonContent, new Uri(inboxUrl));

            _logger.LogInformation($"Sent Follow request to {actorUrl}");
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error sending follow request to {actorUrl}");
            return (false, $"Failed to send follow request: {ex.Message}");
        }
    }

    public async Task SendUnfollowAsync(string actorUrl, string inboxUrl, UserScopedDb db, string actorId)
    {
        if (string.IsNullOrEmpty(actorUrl) || string.IsNullOrEmpty(inboxUrl))
        {
            _logger.LogWarning("Cannot send unfollow: missing actor or inbox URL");
            return;
        }

        try
        {
            var (publicKeyPem, privateKeyPem) = await db.GetActorKeysAsync();
            if (string.IsNullOrEmpty(privateKeyPem))
            {
                _logger.LogWarning("Cannot send unfollow: missing private key");
                return;
            }

            var keyId = $"{actorId}#main-key";

            var followId = $"{actorId}/follow/{Guid.NewGuid()}";
            
            // Create an Undo activity that undoes the Follow
            var undo = new UndoActivity
            {
                Context = "https://www.w3.org/ns/activitystreams",
                Actor = actorId,
                Object = new FollowObject
                {
                    Id = followId,
                    Actor = actorId,
                    Object = actorUrl
                },
                Id = $"{actorId}/undo/follow/{Guid.NewGuid()}"
            };

            var jsonContent = JsonSerializer.Serialize(undo, new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull });

            var helper = new ActorHelper(privateKeyPem, keyId, _logger);
            await helper.SendPostSignedRequest(jsonContent, new Uri(inboxUrl));

            _logger.LogInformation($"Sent Unfollow (Undo) request to {actorUrl}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error sending unfollow request to {actorUrl}");
        }
    }
}

public class AnnounceService
{
    private readonly ILogger<AnnounceService> _logger;
    private readonly FollowService _followService;

    public AnnounceService(ILogger<AnnounceService> logger, FollowService followService)
    {
        _logger = logger;
        _followService = followService;
    }

    public async Task HandleCreateActivityAsync(InboxMessage message, UserScopedDb db, string actorId)
    {
        if (string.IsNullOrEmpty(message.Actor))
        {
            _logger.LogWarning("Create message missing actor");
            return;
        }

        _logger.LogInformation($"Handling Create from {message.Actor}");

        // First, check for badges in this Create activity
        await ProcessBadgesAsync(message, db, actorId);

        // Check if this actor is in our AutoBoost links
        var autoBoostLinks = await db.GetAutoBoostLinksAsync();
        var matchingLink = autoBoostLinks.FirstOrDefault(l => ((string)l["Url"]).TrimEnd('/') == message.Actor.TrimEnd('/'));

        if (matchingLink == null)
        {
            _logger.LogInformation($"Actor {message.Actor} is not in AutoBoost links, ignoring");
            return;
        }

        _logger.LogInformation($"Actor {message.Actor} is marked for AutoBoost, will announce");

        // Check if we're following this actor; if not, try to follow
        var linkId = Convert.ToInt32(matchingLink["Id"]);
        var isFollowing = await db.IsFollowingAsync(linkId);
        if (!isFollowing)
        {
            _logger.LogInformation($"Not following {message.Actor}, attempting to follow");
            // Fetch inbox to send follow request
            var (pubKey, privKey) = await db.GetActorKeysAsync();
            if (!string.IsNullOrEmpty(privKey))
            {
                var keyId = $"{actorId}#main-key";
                var helper = new ActorHelper(privKey, keyId, _logger);
                var remoteActor = await helper.FetchActorInformationAsync(message.Actor);
                if (remoteActor?.Inbox != null)
                {
                    await _followService.SendFollowRequestAsync(message.Actor, remoteActor.Inbox, db, actorId);
                }
            }
        }

        // Announce the Create activity
        await SendAnnounceAsync(message, db, actorId);
    }

    private async Task ProcessBadgesAsync(InboxMessage message, UserScopedDb db, string actorId)
    {
        try
        {
            // Get the Object from the message - it should be the Note/content
            if (message.Object is JsonElement objectElement)
            {
                var objectJson = objectElement.GetRawText();
                using var jsonDoc = JsonDocument.Parse(objectJson);
                var objectRoot = jsonDoc.RootElement;

                // Check if this note has an OpenBadge assertion
                if (!objectRoot.TryGetProperty("openbadges:assertion", out var assertionElement))
                {
                    _logger.LogDebug("No OpenBadge assertion in Create object");
                    return;
                }

                // Check if we're mentioned in the note (To, Cc, Tag) or if any of our links are mentioned
                var userLinks = await db.GetLinksAsync();
                var linkUrls = userLinks.Select(l => (string)l["Url"]).ToList();
                var mentionsUs = IsMentionedInObject(objectRoot, actorId, linkUrls);
                if (!mentionsUs)
                {
                    _logger.LogDebug($"Profile not mentioned in badge note from {message.Actor}");
                    return;
                }

                _logger.LogInformation($"Found badge mentioned for us in Create from {message.Actor}");

                // Get the assertion content
                var assertion = assertionElement.GetRawText();
                using var assertionDoc = JsonDocument.Parse(assertion);
                var assertionRoot = assertionDoc.RootElement;

                // Extract badge details from assertion
                var badgeTitle = objectRoot.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? "Unknown Badge" : "Unknown Badge";
                var badgeImage = objectRoot.TryGetProperty("image", out var imageEl) ? imageEl.GetString() : null;
                var badgeDescription = objectRoot.TryGetProperty("content", out var contentEl) ? contentEl.GetString() : null;
                var badgeIssuedOn = assertionRoot.TryGetProperty("issuedOn", out var issuedEl) ? issuedEl.GetString() : null;

                // Create or get the badge issuer
                var issuerUrl = message.Actor;
                var issuerName = objectRoot.TryGetProperty("attributedTo", out var attrEl) ? attrEl.GetString() ?? message.Actor : message.Actor;
                var issuerAvatar = objectRoot.TryGetProperty("icon", out var iconEl) ? iconEl.GetString() : null;

                var issuerId = await db.CreateOrGetBadgeIssuerAsync(issuerName, issuerUrl, issuerAvatar);

                // Store the badge
                var noteId = message.Id ?? $"{actorId}/note/{Guid.NewGuid()}";
                var badgeRecordId = await db.StoreBadgeAsync(noteId, issuerId, badgeTitle, badgeImage, badgeDescription, badgeIssuedOn);

                if (badgeRecordId > 0)
                {
                    _logger.LogInformation($"Stored badge '{badgeTitle}' from issuer {issuerUrl} (Badge ID: {badgeRecordId})");
                    
                    // Also announce/boost the badge activity
                    await SendAnnounceAsync(message, db, actorId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing badges from Create activity");
        }
    }

    private bool IsMentionedInObject(JsonElement objectElement, string actorId, List<string>? linkUrls = null)
    {
        // Collect all identifiers to check against
        var idsToCheck = new List<string> { actorId };
        if (linkUrls != null)
        {
            idsToCheck.AddRange(linkUrls);
        }

        // Check To field
        if (objectElement.TryGetProperty("to", out var toElement))
        {
            if (toElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in toElement.EnumerateArray())
                {
                    var val = item.GetString();
                    if (val != null && idsToCheck.Any(id => id == val)) return true;
                }
            }
            else
            {
                var val = toElement.GetString();
                if (val != null && idsToCheck.Any(id => id == val)) return true;
            }
        }

        // Check Cc field
        if (objectElement.TryGetProperty("cc", out var ccElement))
        {
            if (ccElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in ccElement.EnumerateArray())
                {
                    var val = item.GetString();
                    if (val != null && idsToCheck.Any(id => id == val)) return true;
                }
            }
            else
            {
                var val = ccElement.GetString();
                if (val != null && idsToCheck.Any(id => id == val)) return true;
            }
        }

        // Check Tag field for mentions
        if (objectElement.TryGetProperty("tag", out var tagElement))
        {
            if (tagElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in tagElement.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("href", out var hrefEl))
                    {
                        var val = hrefEl.GetString();
                        if (val != null && idsToCheck.Any(id => id == val)) return true;
                    }
                }
            }
        }

        // Also check content for mentions of our links
        if (linkUrls != null && objectElement.TryGetProperty("content", out var contentEl))
        {
            var content = contentEl.GetString();
            if (content != null)
            {
                foreach (var linkUrl in linkUrls)
                {
                    if (content.Contains(linkUrl)) return true;
                }
            }
        }

        return false;
    }

    private async Task SendAnnounceAsync(InboxMessage createMessage, UserScopedDb db, string actorId)
    {
        try
        {
            var (publicKeyPem, privateKeyPem) = await db.GetActorKeysAsync();
            if (string.IsNullOrEmpty(privateKeyPem))
            {
                _logger.LogWarning("Cannot announce: missing private key");
                return;
            }

            var keyId = $"{actorId}#main-key";

            var announce = new AnnounceActivity
            {
                Context = "https://www.w3.org/ns/activitystreams",
                Actor = actorId,
                Object = createMessage.Id,
                Id = $"{actorId}/announces/{Guid.NewGuid()}",
                To = new[] { "https://www.w3.org/ns/activitystreams#Public" }
            };

            var jsonContent = JsonSerializer.Serialize(announce, new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull });

            // Deliver the Announce to all followers
            var followers = await db.GetFollowersAsync();
            var helper = new ActorHelper(privateKeyPem, keyId, _logger);
            
            foreach (var follower in followers)
            {
                var inbox = follower.ContainsKey("Inbox") ? follower["Inbox"]?.ToString() : null;
                if (!string.IsNullOrEmpty(inbox))
                {
                    try
                    {
                        await helper.SendPostSignedRequest(jsonContent, new Uri(inbox));
                        _logger.LogInformation($"Sent Announce to follower inbox: {inbox}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Failed to deliver Announce to {inbox}: {ex.Message}");
                    }
                }
            }
            
            _logger.LogInformation($"Announced activity {createMessage.Id} to {followers.Count} followers");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error announcing create activity");
        }
    }
}
