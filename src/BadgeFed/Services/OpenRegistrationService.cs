using System.Text.RegularExpressions;
using BadgeFed.Models;

namespace BadgeFed.Services;

public class OpenRegistrationService
{
    public const string LimitedManagerRole = "manager-limited";

    private readonly LocalScopedDb _db;
    private readonly ILogger<OpenRegistrationService> _logger;

    public OpenRegistrationService(LocalScopedDb db, ILogger<OpenRegistrationService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public bool IsEnabled()
    {
        var instanceDescription = _db.GetInstanceDescription();
        return instanceDescription.OpenRegistrationEnabled ?? false;
    }

    public bool IsProviderEligible(string provider)
    {
        return provider.Equals("LinkedIn", StringComparison.OrdinalIgnoreCase)
            || provider.Equals("Mastodon", StringComparison.OrdinalIgnoreCase);
    }

    public User ProvisionUser(OpenRegistrationIdentity identity)
    {
        var existingUser = _db.GetUserById(identity.UserId);
        if (existingUser != null)
        {
            return existingUser;
        }

        var displayName = GetDisplayName(identity);
        var group = new UserGroup
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = BuildDefaultGroupName(displayName, identity.Provider),
            Description = BuildDefaultGroupDescription(identity),
            CreatedAt = DateTime.UtcNow,
            OnboardingCompleted = false
        };

        _db.UpsertUserGroup(group);

        var user = new User
        {
            Id = identity.UserId,
            Email = identity.Email ?? string.Empty,
            GivenName = identity.GivenName ?? displayName,
            Surname = identity.Surname ?? string.Empty,
            CreatedAt = DateTime.UtcNow,
            Provider = identity.Provider,
            Role = LimitedManagerRole,
            GroupId = group.Id,
            IsActive = true
        };

        _db.UpsertUser(user);
        _logger.LogInformation("Provisioned open-registration user {UserId} with group {GroupId}", user.Id, group.Id);
        return user;
    }

    public bool NeedsOnboarding(string? groupId)
    {
        if (string.IsNullOrWhiteSpace(groupId) || string.Equals(groupId, "system", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var group = _db.GetUserGroupById(groupId);
        return group != null && !group.OnboardingCompleted;
    }

    private static string GetDisplayName(OpenRegistrationIdentity identity)
    {
        var fullName = string.Join(" ", new[] { identity.GivenName, identity.Surname }
            .Where(value => !string.IsNullOrWhiteSpace(value)));

        if (!string.IsNullOrWhiteSpace(fullName))
        {
            return fullName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(identity.DisplayName))
        {
            return identity.DisplayName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(identity.Email))
        {
            return identity.Email.Split('@')[0];
        }

        return identity.UserId;
    }

    private static string BuildDefaultGroupName(string displayName, string provider)
    {
        var sanitizedDisplayName = Regex.Replace(displayName.Trim(), "\\s+", " ");
        return provider.Equals("LinkedIn", StringComparison.OrdinalIgnoreCase)
            ? sanitizedDisplayName
            : $"{sanitizedDisplayName} account";
    }

    private static string BuildDefaultGroupDescription(OpenRegistrationIdentity identity)
    {
        if (!string.IsNullOrWhiteSpace(identity.Email))
        {
            return $"Self-registered from {identity.Provider} for {identity.Email}.";
        }

        return $"Self-registered from {identity.Provider}.";
    }
}

public class OpenRegistrationIdentity
{
    public string UserId { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? GivenName { get; set; }
    public string? Surname { get; set; }
    public string? DisplayName { get; set; }
}