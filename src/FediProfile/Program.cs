using System.Text.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.Extensions.Options;
using FediProfile.Core;
using FediProfile.Models;
using FediProfile.Services;
using FediProfile.Components;
using FediProfile.Identity;

var builder = WebApplication.CreateBuilder(args);

// Setup logging
builder.Services.AddLogging(logging =>
{
    logging.ClearProviders();
    logging.AddConsole();
    logging.AddDebug();
});

builder.Services.AddHttpClient();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddServerSideBlazor();
builder.Services.AddControllers();
builder.Services.AddAuthorization();

// Register multi-tenant, multi-user database factory
var localDbFactory = new LocalDbFactory();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<LocalDbFactory>(localDbFactory);
builder.Services.AddScoped<UserContextAccessor>();

// Register split scoped databases
// DomainScopedDb: One domain database per domain (e.g., localhost.db, example.com.db)
// Contains: users table with OAuth credentials, actor keys, settings
builder.Services.AddScoped<DomainScopedDb>();

// UserScopedDb: Per-user database scoped to current user (e.g., localhost_maho.db)
// Contains: profile information (badges, links, followers, etc.)
// Automatically falls back to DomainScopedDb if user doesn't exist or not in user context
builder.Services.AddScoped<UserScopedDb>();



// Register services
builder.Services.AddSingleton<ActorService>();
builder.Services.AddScoped<FollowService>();
builder.Services.AddScoped<AnnounceService>();

// Add Mastodon registration service
builder.Services.AddScoped<MastodonRegistrationService>(provider =>
{
    var httpClient = provider.GetRequiredService<HttpClient>();
    var config = provider.GetRequiredService<IConfiguration>();
    var domains = config.GetSection("Domains").Get<string[]>()
        ?? new[] { "localhost" };

    var primaryDomain = domains.First();
    var primaryScheme = primaryDomain.Contains("localhost") ? "http" : "https";
    var website = $"{primaryScheme}://{primaryDomain}";

    var redirectUris = new List<string>();
    foreach (var domain in domains)
    {
        Console.WriteLine($"Adding Mastodon redirect URIs for domain: {domain}");
        redirectUris.Add($"https://{domain}/signin-mastodon-dynamic");
        redirectUris.Add($"http://{domain}/signin-mastodon-dynamic");
    }

    return new MastodonRegistrationService(httpClient, "FediProfile", website, redirectUris.ToArray());
});

var auth = builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(option =>
    {
        // Default paths, but will be overridden by user-scoped paths
        option.LoginPath = "/login";
        option.LogoutPath = "/logout";
        option.AccessDeniedPath = "/admin/denied";
    });

auth.AddDynamicMastodon(o =>
{
    o.Scope.Add("read:accounts");
    o.Scope.Add("profile");
    o.SaveTokens = true;
}, localDbFactory);

var app = builder.Build();

app.UseRouting();
app.UseStaticFiles();

// Initialize all domains from configuration
var config = builder.Configuration;
var domains = config.GetSection("Domains").Get<string[]>() ?? new[] { "localhost" };

foreach (var domain in domains)
{
    await InitializeDomainAsync(domain, config, localDbFactory, app.Logger);
}

var actorService = app.Services.GetRequiredService<ActorService>();

// Helper: serve the styled 404 page for browser requests
IResult NotFoundPage(IWebHostEnvironment env)
{
    var notFoundPath = Path.Combine(env.WebRootPath, "404.html");
    if (File.Exists(notFoundPath))
    {
        return Results.Content(File.ReadAllText(notFoundPath), "text/html", statusCode: 404);
    }
    return Results.NotFound("Not found");
}

// Root landing page (/) - modern static page
app.MapGet("/", async (IWebHostEnvironment env) =>
{
    var filePath = Path.Combine(env.WebRootPath, "landing.html");
    if (!File.Exists(filePath))
    {
        return Results.Content(@"
<!DOCTYPE html>
<html>
<head>
    <title>FediProfile</title>
    <meta charset='utf-8' />
    <meta name='viewport' content='width=device-width, initial-scale=1' />
</head>
<body>
    <h1>FediProfile</h1>
    <p>A federated profile system.</p>
</body>
</html>", "text/html");
    }
    return Results.File(filePath, "text/html");
});

// Map ActivityPub actor endpoint: /{user} or /{user}/ 
app.MapGet("/{userSlug}", async (HttpRequest request, string userSlug, LocalDbFactory factory, ActorService actorService, IWebHostEnvironment env) =>
{
    var accept = request.Headers.Accept.ToString();
    var domain = request.Host.Host;
    if (request.Host.Port.HasValue && request.Host.Port != 80 && request.Host.Port != 443)
    {
        domain = $"{domain}:{request.Host.Port}";
    }

    Console.WriteLine($"Received request for /{userSlug} with Accept: {accept}");

    // Check if user exists in main database
    var mainDb = factory.GetInstance(domain);
    var user = await mainDb.GetUserBySlugAsync(userSlug);
    
    if (user == null)
    {
        return NotFoundPage(env);
    }

    // Check if user database file exists
    // Note: User databases are created during user registration with InitializeNewUserAsync
    var checkDb = factory.GetInstance(domain, userSlug, autoCreate: false);
    if (!File.Exists(checkDb.DbPath))
    {
        return NotFoundPage(env);
    }

    // Get the user-specific database
    var userDb = factory.GetInstance(domain, userSlug);

    if (ActivityPubHelper.IsActivityPubRequest(accept))
    {
        var actor = await actorService.BuildActorAsync(userDb, request);
        var jsonString = JsonSerializer.Serialize(actor, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });

        Console.WriteLine($"Returning actor JSON: {jsonString}");
        return Results.Content(jsonString, "application/activity+json");
    }

    var filePath = Path.Combine(env.WebRootPath, "profile.html");
    return Results.File(filePath, "text/html");
});

// Keep .well-known endpoints as they are (not user-scoped for now)
app.MapGet("/.well-known/webfinger", async (HttpRequest request, LocalDbFactory factory) =>
{
    var resource = request.Query["resource"].ToString();

    if (string.IsNullOrEmpty(resource) || !resource.StartsWith("acct:"))
    {
        return Results.BadRequest("Invalid resource parameter");
    }

    var domain = request.Host.Host;
    if (request.Host.Port.HasValue && request.Host.Port != 80 && request.Host.Port != 443)
    {
        domain = $"{domain}:{request.Host.Port}";
    }

    var account = resource.Substring("acct:".Length);
    var parts = account.Split('@');

    if (parts.Length != 2 || parts[1] != domain)
    {
        return Results.NotFound("Account invalid or not found");
    }

    // Query main database for this user
    var mainDb = factory.GetInstance(domain);
    var user = await mainDb.GetUserBySlugAsync(parts[0]);
    
    if (user == null)
    {
        return Results.NotFound("Account or domain not found");
    }

    var username = parts[0];
    var scheme = request.Scheme;
    var actorId = $"{scheme}://{domain}/{username}";
    var subject = $"acct:{username}@{domain}";
    var aliases = new[] { actorId };

    var links = new object[]
    {
        new
        {
            rel = "self",
            type = "application/activity+json",
            href = actorId
        },
        new
        {
            rel = "http://webfinger.net/rel/profile-page",
            type = "text/html",
            href = actorId
        }
    };

    var result = Results.Json(new { subject, aliases, links });
    return result;
});

app.MapGet("/.well-known/nodeinfo", () =>
{
    var domain = "example.com";
    var links = new object[]
    {
        new
        {
            rel = "http://nodeinfo.diaspora.software/ns/schema/2.1",
            href = $"https://{domain}/nodeinfo/2.1"
        }
    };

    var result = Results.Json(new { links });
    return result;
});

// ===== MAIN DOMAIN ADMIN ENDPOINTS =====
// These must be registered BEFORE /{userSlug} routes to avoid routing conflicts


// User-scoped theme endpoint: /{user}/theme.css
app.MapGet("/{userSlug}/theme.dep.css", async (UserScopedDb db, IWebHostEnvironment env) =>
{
    var themeName = await db.GetUiThemeAsync();
    var themePath = Path.Combine(env.WebRootPath, themeName);

    if (!File.Exists(themePath))
    {
        themePath = Path.Combine(env.WebRootPath, "theme-classic.css");
    }

    return Results.File(themePath, "text/css");
});

// User-scoped links endpoint: /{user}/links
app.MapGet("/{userSlug}/links", async (UserScopedDb db) =>
{
    var links = await db.GetLinksAsync();
    return Results.Json(links);
});

// User-scoped badges endpoint: /{user}/badges
app.MapGet("/{userSlug}/badges", async (UserScopedDb db) =>
{
    var badges = await db.GetReceivedBadgesAsync();
    return Results.Json(badges);
});

// User-scoped badge issuers endpoint: /{user}/badge-issuers
app.MapGet("/{userSlug}/badge-issuers", async (UserScopedDb db) =>
{
    var issuers = await db.GetBadgeIssuersAsync();
    return Results.Json(issuers);
});

// ActivityPub followers collection: /{user}/followers
app.MapGet("/{userSlug}/followers", async (HttpRequest request, UserScopedDb db) =>
{
    var accept = request.Headers.Accept.ToString();

    if (!ActivityPubHelper.IsActivityPubRequest(accept))
    {
        return Results.BadRequest("This endpoint only supports ActivityPub requests");
    }

    var domain = request.Host.ToString();
    var scheme = request.Scheme;
    var userSlug = (string)request.RouteValues["userSlug"];

    var emptyCollection = new ActivityPubCollection
    {
        Id = $"{scheme}://{domain}/{userSlug}/followers",
        Type = "Collection",
        TotalItems = 0,
        OrderedItems = Array.Empty<string>()
    };

    try
    {
        var followers = await db.GetFollowersAsync();

        if (followers == null || followers.Count == 0)
        {
            // Return empty collection if no followers
            return Results.Json(emptyCollection, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = false,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });
        }
        
        var followerUris = followers.Select(f => f.FollowerUri).ToList();

        var collection = new ActivityPubCollection
        {
            Id = $"{scheme}://{domain}/{userSlug}/followers",
            Type = "Collection",
            TotalItems = followers.Count,
            OrderedItems = followerUris
        };

        return Results.Json(collection, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });
    } catch (Exception ex)
    {
        Console.WriteLine($"Error in followers endpoint: {ex.Message}");
        // return empty collection
        return Results.Json(emptyCollection, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });
    }
   
});

// ActivityPub following collection: /{user}/following
app.MapGet("/{userSlug}/following", async (HttpRequest request) =>
{
    var accept = request.Headers.Accept.ToString();

    if (!ActivityPubHelper.IsActivityPubRequest(accept))
    {
        return Results.BadRequest("This endpoint only supports ActivityPub requests");
    }

    var domain = request.Host.ToString();
    var scheme = request.Scheme;
    var userSlug = (string)request.RouteValues["userSlug"];

    var collection = new ActivityPubCollection
    {
        Id = $"{scheme}://{domain}/{userSlug}/following",
        Type = "Collection",
        TotalItems = 0,
        OrderedItems = Array.Empty<string>()
    };

    return Results.Json(collection, new System.Text.Json.JsonSerializerOptions
    {
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    });
});

// ActivityPub outbox: /{user}/outbox
app.MapGet("/{userSlug}/outbox", async (HttpRequest request) =>
{
    var accept = request.Headers.Accept.ToString();

    if (!ActivityPubHelper.IsActivityPubRequest(accept))
    {
        return Results.BadRequest("This endpoint only supports ActivityPub requests");
    }

    var domain = request.Host.ToString();
    var scheme = request.Scheme;
    var userSlug = (string)request.RouteValues["userSlug"];

    var collection = new ActivityPubCollection
    {
        Id = $"{scheme}://{domain}/{userSlug}/outbox",
        Type = "OrderedCollection",
        TotalItems = 0,
        OrderedItems = Array.Empty<object>()
    };

    return Results.Json(collection, new System.Text.Json.JsonSerializerOptions
    {
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    });
});

// Add middleware to handle dynamic Mastodon OAuth endpoints
app.Use(async (context, next) =>
{
    if (context.Request.Path == "/signin-mastodon-dynamic" && context.Request.Query.ContainsKey("state"))
    {
        try
        {
            var authService = context.RequestServices.GetService<IAuthenticationSchemeProvider>();
            var scheme = await authService.GetSchemeAsync("DynamicMastodon");
            if (scheme != null)
            {
                var options = context.RequestServices.GetService<IOptionsMonitor<OAuthOptions>>();
                var oauthOptions = options.Get("DynamicMastodon");

                var stateValue = context.Request.Query["state"];
                var properties = oauthOptions.StateDataFormat.Unprotect(stateValue);

                if (properties != null && properties.Items.TryGetValue("mastodon_hostname", out var hostname))
                {
                    oauthOptions.TokenEndpoint = $"https://{hostname}/oauth/token";
                    oauthOptions.UserInformationEndpoint = $"https://{hostname}/api/v1/accounts/verify_credentials";

                    if (properties.Items.TryGetValue("mastodon_client_id", out var clientId))
                    {
                        oauthOptions.ClientId = clientId;
                    }
                    if (properties.Items.TryGetValue("mastodon_client_secret", out var clientSecret))
                    {
                        oauthOptions.ClientSecret = clientSecret;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in dynamic Mastodon middleware: {ex.Message}");
        }
    }

    await next();
});

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapControllers();

// Map login/logout at root level: /login, /logout
app.MapLoginAndLogout();

// Map registration OAuth endpoints
app.MapRegistrationOAuth();

// Fallback: serve styled 404 page for any unmatched route
app.MapFallback((IWebHostEnvironment env) => NotFoundPage(env));

// ===== LOCAL FUNCTION: Domain Initialization =====
/// <summary>
/// Initializes a domain by:
/// 1. Creating/getting the domain database
/// 2. Initializing domain settings from configuration
/// 3. Creating the admin user account with user-specific database
/// 
/// This is extracted as a reusable method to support multi-domain initialization
/// and can be called again when adding single-step account creation.
/// </summary>
async Task InitializeDomainAsync(string domain, IConfiguration config, LocalDbFactory factory, ILogger logger)
{
    // Normalize domain
    domain = domain.Trim().ToLowerInvariant().TrimEnd('/').Split(':')[0];
    
    if (string.IsNullOrWhiteSpace(domain))
    {
        logger.LogWarning("Skipping initialization: Domain is null or empty");
        return;
    }

    logger.LogInformation("Initializing domain: {Domain}", domain);

    // 1. Get or create main database for this domain
    var mainDb = factory.GetInstance(domain);

    var adminMastodonUser = config["AdminAuthentication:MastodonUser"];
    var adminMastodonDomain = config["AdminAuthentication:MastodonDomain"];

    // 2. Initialize admin user in the linktree system
    // The admin linktree user is always "root", matching CONTEXT.md specification
    const string adminUserSlug = "root";

    string adminDisplayName = !string.IsNullOrWhiteSpace(adminMastodonUser) && !string.IsNullOrWhiteSpace(adminMastodonDomain)
        ? $"Admin ({adminMastodonUser}@{adminMastodonDomain})"
        : "Administrator";

    try
    {
        // InitializeNewUserAsync creates:
        // - User entry in main database (domain.db)
        // - User-specific database (domain_root.db) with all default tables
        var (adminUserId, slug) = await mainDb.InitializeNewUserAsync(domain, adminUserSlug, adminDisplayName, adminMastodonUser, adminMastodonDomain);
        
        logger.LogInformation("Initialized admin user '{AdminSlug}' (ID: {AdminUserId}) on domain {Domain} authenticated as {MastodonUser}@{MastodonDomain}",
            slug, adminUserId, domain, adminMastodonUser ?? "unconfigured", adminMastodonDomain ?? "unconfigured");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to initialize admin user '{AdminUserSlug}' on domain {Domain}", adminUserSlug, domain);
    }
}

app.Run();

