namespace BadgeFed.Services;

/// <summary>
/// Abstraction for billing/subscription functionality.
/// The default implementation (NullBillingService) is a no-op that allows all access,
/// keeping the OSS version fully functional without any payment system.
/// A private plugin can replace this by registering its own implementation before
/// NullBillingService is registered (using IHostingStartup + TryAddScoped pattern).
/// </summary>
public interface IBillingService
{
    /// <summary>
    /// Returns true when a real billing plugin is installed and configured.
    /// NullBillingService returns false, letting the UI hide billing-related links.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Returns true if the given user group has an active (paid) subscription.
    /// When no billing plugin is installed the default returns false, so
    /// manager-limited users are always subject to the free-tier caps.
    /// The admin can lift limits by manually upgrading the user's role to
    /// "manager". A billing plugin can instead grant access programmatically.
    /// </summary>
    Task<bool> HasActiveSubscription(string userGroupId);

    /// <summary>
    /// Returns a Stripe Checkout URL to start a new subscription, or null if billing is not configured.
    /// </summary>
    Task<string?> GetCheckoutUrl(string userGroupId, string returnUrl);

    /// <summary>
    /// Returns a Stripe Customer Portal URL to manage an existing subscription, or null if billing is not configured.
    /// </summary>
    Task<string?> GetPortalUrl(string userGroupId, string returnUrl);

    /// <summary>
    /// Returns the current subscription status for a user group, or null if billing is not configured.
    /// Examples: "active", "trialing", "past_due", "canceled", null
    /// </summary>
    Task<string?> GetSubscriptionStatus(string userGroupId);
}

/// <summary>
/// Default no-op implementation used when no billing plugin is installed.
/// HasActiveSubscription returns false so manager-limited users always hit
/// the free-tier caps. The admin upgrades accounts manually by changing the
/// user role from manager-limited to manager in the Users admin panel.
/// </summary>
public class NullBillingService : IBillingService
{
    public bool IsEnabled => false;
    public Task<bool> HasActiveSubscription(string userGroupId) => Task.FromResult(false);
    public Task<string?> GetCheckoutUrl(string userGroupId, string returnUrl) => Task.FromResult<string?>(null);
    public Task<string?> GetPortalUrl(string userGroupId, string returnUrl) => Task.FromResult<string?>(null);
    public Task<string?> GetSubscriptionStatus(string userGroupId) => Task.FromResult<string?>(null);
}
