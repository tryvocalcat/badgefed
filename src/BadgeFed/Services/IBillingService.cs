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
    /// Returns true if the given user group has an active subscription (or if billing is not configured).
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
/// All access is allowed and no Stripe URLs are generated.
/// </summary>
public class NullBillingService : IBillingService
{
    public bool IsEnabled => false;
    public Task<bool> HasActiveSubscription(string userGroupId) => Task.FromResult(true);
    public Task<string?> GetCheckoutUrl(string userGroupId, string returnUrl) => Task.FromResult<string?>(null);
    public Task<string?> GetPortalUrl(string userGroupId, string returnUrl) => Task.FromResult<string?>(null);
    public Task<string?> GetSubscriptionStatus(string userGroupId) => Task.FromResult<string?>(null);
}
