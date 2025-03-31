using System.Security.Claims;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

public class CurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    private readonly ProtectedSessionStorage _protectedSessionStorage;

    public CurrentUser(IHttpContextAccessor httpContextAccessor, ProtectedSessionStorage protectedSessionStorage)
    {
        _httpContextAccessor = httpContextAccessor;
        _protectedSessionStorage = protectedSessionStorage;
    }
    
    public string? Issuer => _httpContextAccessor.HttpContext?.User.Identity.AuthenticationType;

    public string? Id => _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    public string? Email => _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.Email)?.Value;
    public string? Name => _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.Name)?.Value;
    public string? GivenName => _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.GivenName)?.Value;
    public string? Surname => _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.Surname)?.Value;
    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;

    public string? UserId => IsAuthenticated ? string.Concat(Issuer, "_", Id) : null;

    public bool IsAdmin => IsAuthenticated && UserId == "XXX";

    public bool HasClaim(string claimType) => 
        _httpContextAccessor.HttpContext?.User.HasClaim(c => c.Type == claimType) ?? false;
} 