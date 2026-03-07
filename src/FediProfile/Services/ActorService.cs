using FediProfile.Models;

namespace FediProfile.Services;

public class ActorService
{
    public async Task<ActivityPubActor> BuildActorAsync(UserScopedDb db, HttpRequest request, string? userSlug = null)
    {
        // Get username and other settings from database
        var username = await db.GetActorUsernameAsync();
        var domain = request.Host.Host;
        
        if (request.Host.Port.HasValue && request.Host.Port != 80 && request.Host.Port != 443)
        {
            domain = $"{domain}:{request.Host.Port}";
        }

        var scheme = request.Scheme;
        
        // Use userSlug for the actor ID if available, otherwise fall back to route value or "profile"
        var slug = userSlug ?? request.RouteValues["userSlug"]?.ToString() ?? "profile";
        var baseDomain = $"{scheme}://{domain}";
        var baseId = $"{baseDomain}/{slug}";
        var bio = await db.GetActorBioAsync();
        var avatarUrl = await db.GetActorAvatarUrlAsync();
        if (!string.IsNullOrEmpty(avatarUrl) && avatarUrl.StartsWith("/"))
            avatarUrl = $"{baseDomain}{avatarUrl}";

        // Get actor keys for publicKey field
        var (pubKey, privKey) = await db.GetActorKeysAsync();

        var links = await db.GetLinksAsync();
        // Filter out hidden links and format as HTML attachments
        var attachments = links
            .Cast<Dictionary<string, object>>()
            .Where(link => link.TryGetValue("Hidden", out var h) && Convert.ToInt64(h) == 0)
            .Select(link => 
            {
                var url = link["Url"]?.ToString() ?? "";
                var name = link["Name"]?.ToString() ?? "";
                var icon = link.TryGetValue("Icon", out var ic) ? ic?.ToString() : null;
                if (!string.IsNullOrEmpty(icon) && icon.StartsWith("/"))
                    icon = $"{baseDomain}{icon}";
                var category = link.TryGetValue("Category", out var cat) ? cat?.ToString() : null;
                var description = link.TryGetValue("Description", out var desc) ? desc?.ToString() : null;
                var autoBoost = link.TryGetValue("AutoBoost", out var ab) && Convert.ToInt64(ab) != 0;

                var uri = new Uri(url);
                var displayUrl = uri.Host + uri.PathAndQuery;
                var invisibleStart = displayUrl.Length > 30 ? 
                    $"<span class=\"invisible\">https://</span><span class=\"ellipsis\">{displayUrl.Substring(0, 30)}</span>" :
                    $"<span class=\"invisible\">https://</span><span class=\"\">{displayUrl}</span>";
                
                var htmlValue = $"<a href=\"{url}\" target=\"_blank\" rel=\"nofollow noopener me\" translate=\"no\">{invisibleStart}<span class=\"invisible\"></span></a>";
                
                return new LinkAttachment
                {
                    Name = name,
                    Value = htmlValue,
                    Href = url,
                    Icon = !string.IsNullOrEmpty(icon) ? new ActivityPubImage { Type = "Image", Url = icon } : null,
                    Category = category,
                    Description = description,
                    AutoBoost = autoBoost ? true : null
                };
            })
            .ToList();

        return new ActivityPubActor
        {
            Context = "https://www.w3.org/ns/activitystreams",
            Id = baseId,
            Type = "Person",
            PreferredUsername = slug,
            Name = username,
            Summary = bio,
            Url = baseId,
            Inbox = $"{baseDomain}/sharedInbox",
            Outbox = $"{baseId}/outbox",
            Followers = $"{baseId}/followers",
            Following = $"{baseId}/following",
            PublicKey = new PublicKeyDefinition
            {
                Id = $"{baseId}#main-key",
                Owner = baseId,
                PublicKeyPem = pubKey ?? string.Empty
            },
            Discoverable = true,
            Icon = new ActivityPubImage
            {
                Type = "Image",
                MediaType = "image/png",
                Url = avatarUrl
            },
            Image = new ActivityPubImage
            {
                Type = "Image",
                MediaType = "image/png",
                Url = avatarUrl
            },
            Attachment = attachments.Count > 0 ? attachments : null
        };
    }
}
