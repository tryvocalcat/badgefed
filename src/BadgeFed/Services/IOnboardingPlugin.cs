namespace BadgeFed.Services;

/// <summary>
/// Optional plugin hook for custom onboarding experiences.
/// When no implementation is registered the default AdminOnboarding.razor
/// page is used. A plugin can register an implementation to redirect
/// users to a custom wizard instead.
/// 
/// Follow the same pattern as IBillingService: register your implementation
/// via IHostingStartup before the default NullOnboardingPlugin is registered
/// (using ASPNETCORE_HOSTINGSTARTUPASSEMBLIES env var).
/// </summary>
public interface IOnboardingPlugin
{
    /// <summary>
    /// Whether the plugin is active and should intercept onboarding.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Returns the route the user should be redirected to for onboarding,
    /// or null to fall through to the default page.
    /// </summary>
    string? GetOnboardingRoute(string userId, string groupId);

    /// <summary>
    /// Called when the wizard finishes so the core marks the UserGroup as
    /// onboarding-complete. The plugin calls this from its custom wizard page.
    /// </summary>
    Task CompleteOnboardingAsync(string groupId, string groupName, string? description);
}

/// <summary>
/// Default no-op implementation used when no onboarding plugin is installed.
/// Falls through to the standard AdminOnboarding.razor form.
/// </summary>
public class NullOnboardingPlugin : IOnboardingPlugin
{
    public bool IsEnabled => false;
    public string? GetOnboardingRoute(string userId, string groupId) => null;
    public Task CompleteOnboardingAsync(string groupId, string groupName, string? description)
        => Task.CompletedTask;
}
