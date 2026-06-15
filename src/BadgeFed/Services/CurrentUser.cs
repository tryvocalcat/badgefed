using System.Security.Claims;
using BadgeFed.Services;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

public class CurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    private readonly ProtectedSessionStorage _protectedSessionStorage;

    private readonly IBillingService _billingService;

    private readonly ImpersonationService _impersonationService;

    public CurrentUser(IHttpContextAccessor httpContextAccessor, ProtectedSessionStorage protectedSessionStorage, IBillingService billingService, ImpersonationService impersonationService)
    {
        _httpContextAccessor = httpContextAccessor;
        _protectedSessionStorage = protectedSessionStorage;
        _billingService = billingService;
        _impersonationService = impersonationService;
    }

    public string? Issuer => _httpContextAccessor.HttpContext?.User.Identity.AuthenticationType;

    public string? Id => _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    public string? Email => _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.Email)?.Value;
    public string? Name => _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.Name)?.Value;
    public string? GivenName => _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.GivenName)?.Value;
    public string? Surname => _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.Surname)?.Value;
    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;

    public string? UserId => IsAuthenticated ? string.Concat(Issuer?.ToLowerInvariant(), "_", Id) : null;

    public bool HasClaim(string claimType) =>
        _httpContextAccessor.HttpContext?.User.HasClaim(c => c.Type == claimType) ?? false;

    public string GetRole()
    {
        // When impersonating, return the target user's role
        var session = GetImpersonationSession();
        if (session != null)
            return session.TargetRole;

        var roleClaim = _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.Role);
        return roleClaim?.Value ?? string.Empty;
    }

    /// <summary>
    /// Returns the real role from claims, ignoring impersonation.
    /// Used to ensure the admin can always stop impersonation.
    /// </summary>
    public string GetRealRole()
    {
        var roleClaim = _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.Role);
        return roleClaim?.Value ?? string.Empty;
    }

    public string GetGroupId()
    {
        // When impersonating, return the target user's group
        var session = GetImpersonationSession();
        if (session != null)
            return session.TargetGroupId;

        var groupClaim = _httpContextAccessor.HttpContext?.User.FindFirst("urn:badgefed:group");
        return groupClaim?.Value ?? "system";
    }

    public bool IsAdmin()
    {
        // Returns false during impersonation so data filtering works correctly
        if (IsImpersonating)
            return false;
        return IsRealAdmin();
    }

    /// <summary>
    /// Always returns the real admin status from claims, ignoring impersonation.
    /// Used for impersonation banner visibility and stop controls.
    /// </summary>
    public bool IsRealAdmin()
    {
        var role = GetRealRole();
        return !string.IsNullOrEmpty(role) && role.Equals("admin", StringComparison.OrdinalIgnoreCase);
    }

    public bool IsImpersonating => GetImpersonationSession() != null;

    public string? ImpersonatingUserName => GetImpersonationSession()?.TargetUserName;

    private ImpersonationSession? GetImpersonationSession()
    {
        var userId = UserId;
        if (string.IsNullOrEmpty(userId))
            return null;
        return _impersonationService.GetSession(userId);
    }

    public bool CanManage()
    {
        var role = GetRole();
        return !string.IsNullOrEmpty(role)
            && (role.Equals("manager", StringComparison.OrdinalIgnoreCase)
                || role.Equals(OpenRegistrationService.LimitedManagerRole, StringComparison.OrdinalIgnoreCase))
            || IsAdmin();
    }

    public bool CanCollaborate()
    {
        var role = GetRole();
        return !string.IsNullOrEmpty(role) && role.Equals("collaborator", StringComparison.OrdinalIgnoreCase) || CanManage();
    }

    /// <summary>
    /// Returns true if the current user's group has an active (paid) subscription.
    /// Without a billing plugin this is always false, so manager-limited caps always apply.
    /// </summary>
    public Task<bool> HasActiveSubscription() =>
        _billingService.HasActiveSubscription(GetGroupId());
} 