using BadgeFed.Models;

namespace BadgeFed.Services;

public class RecipientProfileService
{
    private readonly LocalScopedDb _db;
    private readonly ICustomAssetPathService _assetPathService;
    private readonly ILogger<RecipientProfileService> _logger;

    public RecipientProfileService(
        LocalScopedDb db,
        ICustomAssetPathService assetPathService,
        ILogger<RecipientProfileService> logger)
    {
        _db = db;
        _assetPathService = assetPathService;
        _logger = logger;
    }

    /// <summary>
    /// Loads a recipient profile with all badges aggregated across linked identities.
    /// </summary>
    public (Recipient? Recipient, List<BadgeRecord> Badges) GetProfileBySlug(string slug)
    {
        var recipient = _db.GetRecipientBySlug(slug);
        if (recipient == null) return (null, new());

        var badges = GetAggregatedBadges(recipient.Id);
        return (recipient, badges);
    }

    /// <summary>
    /// Gets all badges across all linked/merged recipients.
    /// </summary>
    public List<BadgeRecord> GetAggregatedBadges(long recipientId)
    {
        var linkedRecipients = _db.GetLinkedRecipients(recipientId);
        var allBadges = new List<BadgeRecord>();

        foreach (var linked in linkedRecipients)
        {
            if (string.IsNullOrEmpty(linked.ProfileUri)) continue;
            var escapedUri = linked.ProfileUri.Replace("'", "''").ToLower();
            var filter = $"LOWER(br.IssuedToSubjectUri) = '{escapedUri}' AND br.FingerPrint IS NOT NULL AND br.NoteId IS NOT NULL AND br.Visibility = 'Public' AND br.RevokedAt IS NULL";
            var badges = _db.GetBadgeRecords(filter, includeBadge: true);
            allBadges.AddRange(badges);
        }

        return allBadges
            .OrderByDescending(b => b.IssuedOn)
            .DistinctBy(b => b.Id)
            .ToList();
    }

    /// <summary>
    /// Updates recipient profile fields. Validates template and generates slug if needed.
    /// </summary>
    public void UpdateProfile(Recipient recipient)
    {
        var validTemplates = new[] { "professional", "gamification", "minimal" };
        if (!validTemplates.Contains(recipient.ProfileTemplate))
        {
            recipient.ProfileTemplate = "professional";
        }

        if (string.IsNullOrEmpty(recipient.Slug))
        {
            recipient.Slug = _db.GenerateRecipientSlug(
                recipient.DisplayName ?? recipient.Name);
        }

        _db.UpdateRecipientProfile(recipient);
    }

    /// <summary>
    /// Checks if a merge candidate exists when a new OAuth identity is linked.
    /// Returns the "other" recipient that could be merged.
    /// </summary>
    public Recipient? FindMergeCandidate(long currentRecipientId, string? profileUrl, string? email)
    {
        Recipient? candidate = null;

        if (!string.IsNullOrEmpty(profileUrl))
        {
            candidate = _db.GetRecipientByProfileUri(profileUrl);
        }

        if (candidate == null && !string.IsNullOrEmpty(email))
        {
            candidate = _db.GetRecipientByEmail(email);
        }

        if (candidate != null && candidate.Id != currentRecipientId)
        {
            return candidate;
        }

        return null;
    }

    /// <summary>
    /// Merges a secondary recipient into a primary. Sets PrimaryRecipientId.
    /// </summary>
    public void Merge(long primaryId, long secondaryId)
    {
        _db.MergeRecipients(primaryId, secondaryId);
        _logger.LogInformation("Merged recipient {SecondaryId} into primary {PrimaryId}", secondaryId, primaryId);
    }

    /// <summary>
    /// Unmerges a secondary recipient from its primary.
    /// </summary>
    public void Unmerge(long secondaryId)
    {
        _db.UnmergeRecipient(secondaryId);
        _logger.LogInformation("Unmerged recipient {SecondaryId}", secondaryId);
    }

    /// <summary>
    /// Gets all linked OAuth accounts for a recipient.
    /// </summary>
    public List<RecipientIdentity> GetLinkedAccounts(long recipientId)
    {
        return _db.GetRecipientIdentitiesByRecipientId(recipientId);
    }

    /// <summary>
    /// Gets the effective primary recipient for display purposes.
    /// </summary>
    public Recipient GetPrimaryRecipient(long recipientId)
    {
        var recipient = _db.GetRecipientById(recipientId);
        if (recipient?.PrimaryRecipientId != null)
        {
            return _db.GetRecipientById(recipient.PrimaryRecipientId.Value) ?? recipient;
        }
        return recipient!;
    }

    /// <summary>
    /// Gets the asset path for avatar uploads.
    /// </summary>
    public string GetAvatarsPath() => _assetPathService.GetAvatarsPath();

    /// <summary>
    /// Gets the URL for an uploaded avatar image.
    /// </summary>
    public string GetAvatarUrl(string fileName) => _assetPathService.GetAvatarUrl(fileName);
}
