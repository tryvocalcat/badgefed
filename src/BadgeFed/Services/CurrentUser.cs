using System.Security.Claims;
using BadgeFed.Services;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

public class CurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    private readonly ProtectedSessionStorage _protectedSessionStorage;

    private readonly IBillingService _billingService;

    public CurrentUser(IHttpContextAccessor httpContextAccessor, ProtectedSessionStorage protectedSessionStorage, IBillingService billingService)
    {
        _httpContextAccessor = httpContextAccessor;
        _protectedSessionStorage = protectedSessionStorage;
        _billingService = billingService;
    }

    public string? Issuer => _httpContextAccessor.HttpContext?.User.Identity.AuthenticationType;

    public string? Id => _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    public string? Email => _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.Email)?.Value;
    public string? Name => _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.Name)?.Value;
    public string? GivenName => _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.GivenName)?.Value;
    public string? Surname => _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.Surname)?.Value;
    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;

    public string? UserId => IsAuthenticated ? string.Concat(Issuer, "_", Id) : null;

    public bool HasClaim(string claimType) =>
        _httpContextAccessor.HttpContext?.User.HasClaim(c => c.Type == claimType) ?? false;

    public string GetRole()
    {
        var roleClaim = _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.Role);
        return roleClaim?.Value ?? string.Empty;
    }

    public string GetGroupId()
    {
        var groupClaim = _httpContextAccessor.HttpContext?.User.FindFirst("urn:badgefed:group");
        return groupClaim?.Value ?? "system";
    }

    public bool IsAdmin()
    {
        var role = GetRole();
        return !string.IsNullOrEmpty(role) && role.Equals("admin", StringComparison.OrdinalIgnoreCase);
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
    /// Returns true if the current user's group has an active subscription.
    /// Always returns true when no billing plugin is installed.
    /// </summary>
    public Task<bool> HasActiveSubscription() =>
        _billingService.HasActiveSubscription(GetGroupId());
} 