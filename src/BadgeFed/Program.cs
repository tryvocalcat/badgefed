using ActivityPubDotNet.Core;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;
using System.Security.Claims;
using System.Text.Json;
using BadgeFed.Services;
using BadgeFed;
using BadgeFed.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using BadgeFed.Components;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.StaticFiles;
using System.Text;
using BadgeFed.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLogging(logging =>
{
    logging.ClearProviders();
    logging.AddConsole();
    logging.AddDebug();
    logging.AddEventSourceLogger();

    // Optional: Configure log levels
    logging.SetMinimumLevel(LogLevel.Information);
});

builder.Services.AddHttpClient();

builder.Services.AddRazorPages();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddServerSideBlazor();
builder.Services.AddControllers();

// Register the landing page cache service as a singleton
builder.Services.AddSingleton<LandingPageCacheService>();

// Add CORS for embed widget
builder.Services.AddCors(options =>
{
    options.AddPolicy("EmbedPolicy", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});


var localDbFactory = new LocalDbFactory();

builder.Services.AddHttpContextAccessor();

builder.Services.AddSingleton<LocalDbFactory>(localDbFactory);
builder.Services.AddScoped<LocalScopedDb>();
builder.Services.AddScoped<FollowService>();
builder.Services.AddScoped<ExternalBadgeService>();
builder.Services.AddScoped<RepliesService>();
builder.Services.AddScoped<CreateNoteService>();
builder.Services.AddScoped<ServerDiscoveryService>();

var adminConfig = builder.Configuration.GetSection("AdminAuthentication").Get<AdminConfig>();
builder.Services.AddSingleton<AdminConfig>(adminConfig);

builder.Services.AddScoped<DatabaseMigrationService>();

builder.Services.AddScoped<BadgeProcessor>();

builder.Services.AddScoped<CurrentUser>();
builder.Services.AddHttpClient();

builder.Services.AddScoped<OpenBadgeService>();

builder.Services.AddScoped<BadgeImageService>();

builder.Services.AddScoped<BadgeService>();

builder.Services.AddScoped<BadgeGrantService>();

builder.Services.AddScoped<InvitationService>();

builder.Services.AddScoped<RegistrationService>();

builder.Services.AddScoped<UserService>();

// Add custom asset path service
builder.Services.AddScoped<ICustomAssetPathService, CustomAssetPathService>();

// Add Mastodon registration service
builder.Services.AddScoped<MastodonRegistrationService>(provider =>
{
    var httpClient = provider.GetRequiredService<HttpClient>();
    var config = provider.GetRequiredService<IConfiguration>();
    var domains = config.GetSection("BadgesDomains").Get<string[]>() ?? new[] { "localhost:5000" };
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(domains));
    var primaryDomain = domains.First();
    Console.WriteLine(primaryDomain);
    var primaryScheme = primaryDomain.Contains("localhost") ? "http" : "https";
    var website = $"{primaryScheme}://{primaryDomain}";
    
    // Create redirect URIs for both HTTP and HTTPS schemes for each domain
    var redirectUris = new List<string>();
    foreach (var domain in domains)
    {
        redirectUris.Add($"https://{domain}/signin-mastodon-dynamic");
        redirectUris.Add($"http://{domain}/signin-mastodon-dynamic");
    }
    
    return new MastodonRegistrationService(httpClient, "BadgeFed", website, redirectUris.ToArray());
});

// Add a new configuration section for LinkedIn OAuth
var linkedInConfig = builder.Configuration.GetSection("LinkedInConfig").Get<LinkedInConfig>();
var googleConfig = builder.Configuration.GetSection("GoogleConfig").Get<GoogleConfig>();
var mastodonConfig = builder.Configuration.GetSection("MastodonConfig").Get<MastodonConfig>();
var gotoSocialConfig = builder.Configuration.GetSection("GotoSocialConfig").Get<GotoSocialConfig>();

var auth = builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
.AddCookie(option =>
{
    option.LoginPath = "/admin/login";
    option.LogoutPath = "/admin/logout";
    option.AccessDeniedPath = "/admin/denied";
});


// Always add dynamic Mastodon support (no pre-configuration needed)
auth.AddDynamicMastodon(adminConfig, o => {
    o.Scope.Add("read:accounts");
    o.Scope.Add("profile");
    o.SaveTokens = true;
}, localDbFactory);

if (gotoSocialConfig != null)
{
    Console.WriteLine($"GotoSocial configuration found. {System.Text.Json.JsonSerializer.Serialize(gotoSocialConfig)}");
    // If client id is not configured, attempt to register a new application on the GotoSocial instance
    if (string.IsNullOrEmpty(gotoSocialConfig.ClientId))
    {
        try
        {
            Console.WriteLine("GotoSocial client_id not configured, attempting to register application...");
            var domains = builder.Configuration.GetSection("BadgesDomains").Get<string[]>() ?? new[] { "localhost:5000" };
            var registration = await RegisterGotoSocialAppAsync(gotoSocialConfig.Server, builder.Environment.ApplicationName ?? "BadgeFed", domains);
            if (!string.IsNullOrEmpty(registration.clientId))
            {
                gotoSocialConfig.ClientId = registration.clientId;
                gotoSocialConfig.ClientSecret = registration.clientSecret;
                Console.WriteLine("GotoSocial app registered successfully (client_id set in memory).");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to register GotoSocial app: {ex.Message}");
        }
    }

    if (gotoSocialConfig != null && !string.IsNullOrEmpty(gotoSocialConfig.ClientId))
    {
        auth.AddGotoSocial(adminConfig, gotoSocialConfig, o =>
        {
            o.Scope.Add("profile");
            o.ClientId = gotoSocialConfig.ClientId;
            o.ClientSecret = gotoSocialConfig.ClientSecret;
            o.SaveTokens = true;
        }, localDbFactory);
    }
}

if (linkedInConfig != null)
{
    auth.AddLinkedIn(adminConfig, linkedInConfig, o =>
        {
            o.ClientId = linkedInConfig.ClientId;
            o.ClientSecret = linkedInConfig.ClientSecret;
            o.CallbackPath = "/signin-linkedin";
            o.SaveTokens = true;
        }, localDbFactory);
}

if (googleConfig != null)
{
    auth.AddGoogle(adminConfig, googleConfig, o =>
        {
            o.ClientId = googleConfig.ClientId;
            o.ClientSecret = googleConfig.ClientSecret;
            o.CallbackPath = "/signin-google";
            o.SaveTokens = true;
         }, localDbFactory);
}

builder.Services.AddHostedService<JobExecutor>();
builder.Services.AddScoped<JobProcessor>();

// Configure EmailSettings from appsettings.json
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));

builder.Services.AddScoped<MailService>();

builder.Services.AddControllers();

var provider = new FileExtensionContentTypeProvider();

provider.Mappings[".db"] = "application/x-msdownload";

builder.Services.Configure<StaticFileOptions>(options =>
{
    options.ContentTypeProvider = provider;
});

var app = builder.Build();

try
{
    // Setup default actor on first run
    await SetupDefaultActor(app.Services);
}
catch (Exception e)
{
    Console.WriteLine(e);
}

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseStaticFiles();

app.UseCors("EmbedPolicy");
app.UseCookiePolicy();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
    
app.MapControllers();

var loginSchemas = new List<string>();

if (mastodonConfig != null && !string.IsNullOrEmpty(mastodonConfig.Server))
{
    loginSchemas.Add(mastodonConfig.Server);
}

if (gotoSocialConfig != null && !string.IsNullOrEmpty(gotoSocialConfig.Server))
{
    loginSchemas.Add(gotoSocialConfig.Server);
}

app.MapGroup("/admin").MapLoginAndLogout(loginSchemas);

app.Run();

async Task SetupDefaultActor(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

    // Get all domains from configuration
    var domains = configuration.GetSection("BadgesDomains").Get<string[]>() ?? new[] { "example.com" };
    var defaultUsername = "badgerelay";

    foreach (var domain in domains)
    {
        Console.WriteLine($"Setting up default actor for domain: {domain}");
        // domain could be localhost:5000 we need to take just the hostname portion of it
        var localDbService = new LocalScopedDb(domain);
        var existingActor = localDbService.GetActorByFilter($"Username = \"{defaultUsername}\" AND Domain = \"{domain}\"");
        if (existingActor == null)
        {
            // Generate public/private key pair
            var keyPair = await CryptoService.GenerateKeyPairAsync();

            // Create the default actor
            var defaultActor = new Actor
            {
                FullName = $"{domain.ToTitleCase()} Relay Bot",
                Username = defaultUsername,
                Domain = domain,
                Summary = $"Official relay bot for badge announcements on {domain}. Automatically boosts badge grants and credential updates to help spread the word about achievements across the ActivityPub network.",
                PublicKeyPem = keyPair.PublicKeyPem,
                PrivateKeyPem = keyPair.PrivateKeyPem,
                InformationUri = $"https://{domain}/about",
                AvatarPath = "img/defaultavatar.png",
                IsMain = true
            };

            localDbService.UpsertActor(defaultActor);
            Console.WriteLine($"Default actor created with username '{defaultUsername}' and domain '{domain}'.");
        }
    }
}

static async Task<(string clientId, string clientSecret)> RegisterGotoSocialAppAsync(string hostname, string clientName, IEnumerable<string> siteDomains)
{
    if (string.IsNullOrEmpty(hostname)) throw new ArgumentException("Hostname is required", nameof(hostname));
    var host = hostname.Trim().TrimEnd('/');
    var url = $"https://{host}/api/v1/apps";
    using var http = new HttpClient();

    // Add a user agent header
    http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("BadgeFed", "1.0"));

    var redirectUris = new List<string> { "urn:ietf:wg:oauth:2.0:oob" };

    foreach (var d0 in siteDomains ?? Array.Empty<string>())
    {
        if (string.IsNullOrWhiteSpace(d0)) continue;
        var d = d0.Trim().TrimEnd('/');

        // If a scheme is provided, use it; otherwise default to http for localhost and https otherwise
        if (d.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || d.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            if (Uri.TryCreate(d, UriKind.Absolute, out var parsed))
            {
                d = parsed.GetLeftPart(UriPartial.Authority).TrimEnd('/');
            }
        }
        else
        {
            var scheme = d.Contains("localhost") ? "http" : "https";
            d = $"{scheme}://{d}";
        }

        // add common callback paths and the provider-specific signin paths
        redirectUris.Add($"{d}/authentication/oauth/gotosocial");
        redirectUris.Add($"{d}/signin-gotosocial-{host}");
    }

    var payload = new
    {
        client_name = clientName,
        redirect_uris = string.Join("\n", redirectUris.Distinct()),
        scopes = "read profile"
    };

    Console.WriteLine($"GotoSocial registration request to {url}: {System.Text.Json.JsonSerializer.Serialize(payload)}");

    var content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");
    var resp = await http.PostAsync(url, content);
    resp.EnsureSuccessStatusCode();
    var body = await resp.Content.ReadAsStringAsync();

    using var doc = JsonDocument.Parse(body);
    var root = doc.RootElement;
    var clientId = root.TryGetProperty("client_id", out var cid) ? cid.GetString() : null;
    var clientSecret = root.TryGetProperty("client_secret", out var cs) ? cs.GetString() : null;

    Console.WriteLine($"GotoSocial registration response: client_id={(clientId ?? "<null>")}, client_secret={(clientSecret != null ? "<hidden>" : "<null>")}");

    return (clientId, clientSecret);
}


public static class StringExtensions
{
    public static string ToTitleCase(this string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        return char.ToUpper(input[0]) + input.Substring(1).ToLower();
    }
}

public static class MastodonOAuthExtensions
{
    private static readonly HashSet<string> _hosts = new();

    public static IEnumerable<string> Hosts => _hosts;

    public static AuthenticationBuilder AddGotoSocial(
        this AuthenticationBuilder builder,
        AdminConfig adminConfig,
        GotoSocialConfig gotoSocialConfig,
        Action<OAuthOptions> configureOptions,
        LocalDbFactory localDbFactory)
    {
        var hostname = gotoSocialConfig.Server;
        _hosts.Add(hostname);
        return builder.AddOAuth(hostname, $"GotoSocial-{hostname}", o =>
        {
            /* if (string.IsNullOrEmpty(hostname) || Uri.CheckHostName(hostname) == UriHostNameType.Unknown)
             {
                 throw new ArgumentException("Invalid hostname", nameof(hostname));
             }*/

            o.AuthorizationEndpoint = $"https://{hostname}/oauth/authorize";
            o.TokenEndpoint = $"https://{hostname}/oauth/token";
            o.UserInformationEndpoint = $"https://{hostname}/api/v1/accounts/verify_credentials";
            o.CallbackPath = new Microsoft.AspNetCore.Http.PathString($"/signin-gotosocial-{hostname}");

            o.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
            o.ClaimActions.MapJsonKey(ClaimTypes.Name, "username");
            o.ClaimActions.MapJsonKey($"urn:gotosocial:id", "id");
            o.ClaimActions.MapJsonKey($"urn:gotosocial:username", "username");

            o.Events = new OAuthEvents
            {
                OnCreatingTicket = async context =>
                {
                    Console.WriteLine($"GotoSocial OnCreatingTicket for {hostname}");

                    var localDbService = localDbFactory.GetInstance(context.HttpContext);
                    var request = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.AccessToken);
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    var response = await context.Backchannel.SendAsync(request, context.HttpContext.RequestAborted);
                    response.EnsureSuccessStatusCode();

                    if (context.Options.SaveTokens)
                    {
                        context.Properties.StoreTokens(new[] {
                            new AuthenticationToken { Name = "access_token", Value = context.AccessToken }
                        });
                    }

                    using var user = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                    context.RunClaimActions(user.RootElement);

                    var username = context.Identity?.FindFirst(ClaimTypes.Name)?.Value;

                    var isAdmin = adminConfig?.AdminUsers?.Any(a =>
                        a.Type.Equals("GotoSocial", StringComparison.OrdinalIgnoreCase) &&
                        a.Id == username) ?? false;

                    Console.WriteLine($"Is admin: {username} {isAdmin}");

                    // suppor OAuth state, and also cookies
                    var invitationCode = context.Properties.Items.ContainsKey("invitationCode")
                        ? context.Properties.Items["invitationCode"]
                        : context.HttpContext.Request.Cookies["invitationCode"];

                    Console.WriteLine($"Invitation code: {(invitationCode != null ? invitationCode : "<none>")}");

                    if (isAdmin)
                    {
                        context.Principal.AddIdentity(new ClaimsIdentity(new[] {
                            new Claim("urn:gotosocial:hostname", hostname),
                            new Claim(ClaimTypes.Role, "admin"),
                            new Claim("urn:badgefed:group", "system")
                        }));
                    }
                    else
                    {
                        var userId = $"{hostname}_{username}";
                        Console.WriteLine($"User ID: {userId}");

                        var registeredUser = localDbService.GetUserById(userId);

                        if (!string.IsNullOrEmpty(invitationCode) && registeredUser == null)
                        {
                            var invitationService = context.HttpContext.RequestServices.GetRequiredService<InvitationService>();
                            var invitation = invitationService.ValidateAndGetInvitation(invitationCode);

                            if (invitation != null)
                            {
                                registeredUser = new User
                                {
                                    Id = userId,
                                    Email = invitation.Email,
                                    GivenName = username ?? "User",
                                    Surname = "",
                                    CreatedAt = DateTime.UtcNow,
                                    Provider = "GotoSocial",
                                    Role = invitation.Role,
                                    GroupId = invitation.GroupId,
                                    IsActive = true
                                };

                                invitationService.AcceptInvitation(invitationCode, registeredUser);
                                Console.WriteLine($"User created from invitation: {userId} with role {invitation.Role} and group {invitation.GroupId}");
                            }
                        }

                        var role = registeredUser != null ? registeredUser.Role : "user";

                        context.Principal.AddIdentity(new ClaimsIdentity(new[] {
                            new Claim("urn:gotosocial:hostname", hostname),
                            new Claim(ClaimTypes.Role, role),
                            new Claim("urn:badgefed:group", registeredUser?.GroupId ?? "system")
                        }));
                    }
                },
                OnRemoteFailure = context =>
                {
                    Console.WriteLine($"GotoSocial OnRemoteFailure for {hostname} - Error: {context.Failure}");
                    context.HandleResponse();
                    context.Response.Redirect("/admin/denied");
                    return Task.FromResult(0);
                }
            };

            configureOptions(o);
        });
    }

    // In-memory cache for registered Mastodon apps per server
    private static readonly Dictionary<string, (string clientId, string clientSecret)> _mastodonAppCache = new();
    private static readonly SemaphoreSlim _registrationSemaphore = new(1, 1);

    public static AuthenticationBuilder AddDynamicMastodon(
        this AuthenticationBuilder builder,
        AdminConfig adminConfig,
        Action<OAuthOptions> configureOptions,
        LocalDbFactory localDbFactory)
    {
        return builder.AddOAuth("DynamicMastodon", "Mastodon (Dynamic)", o =>
        {
            // Placeholder endpoints - will be overridden dynamically
            o.AuthorizationEndpoint = "https://placeholder.invalid/oauth/authorize";
            o.TokenEndpoint = "https://placeholder.invalid/oauth/token";
            o.UserInformationEndpoint = "https://placeholder.invalid/api/v1/accounts/verify_credentials";
            o.CallbackPath = new Microsoft.AspNetCore.Http.PathString("/signin-mastodon-dynamic");

            // Placeholder credentials - will be set dynamically
            o.ClientId = "placeholder";
            o.ClientSecret = "placeholder";

            o.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
            o.ClaimActions.MapJsonKey(ClaimTypes.Name, "username");
            o.ClaimActions.MapJsonKey($"urn:mastodon:id", "id");
            o.ClaimActions.MapJsonKey($"urn:mastodon:username", "username");

            o.Events = new OAuthEvents
            {
                OnRedirectToAuthorizationEndpoint = async context =>
                {
                    // Extract server from the authentication properties
                    var hostname = context.Properties.Items.TryGetValue("mastodon_server", out var storedServer) 
                        ? storedServer 
                        : throw new InvalidOperationException("Mastodon server not specified");
                    
                    // Get or register OAuth app for this server
                    var (clientId, clientSecret) = await GetOrRegisterMastodonAppAsync(hostname, context.HttpContext.RequestServices);
                    
                    // Store credentials for the callback
                    context.Properties.Items["mastodon_client_id"] = clientId;
                    context.Properties.Items["mastodon_client_secret"] = clientSecret;
                    
                    // Build authorization URL with real credentials
                    var authUrl = $"https://{hostname}/oauth/authorize";
                    
                    // Build the full redirect URI to match what was registered
                    var request = context.HttpContext.Request;
                    var fullRedirectUri = $"{request.Scheme}://{request.Host}{context.Options.CallbackPath}";
                    
                    var queryParams = new Dictionary<string, string>
                    {
                        ["client_id"] = clientId,
                        ["redirect_uri"] = fullRedirectUri,
                        ["response_type"] = "code",
                        ["scope"] = string.Join(" ", context.Options.Scope)
                    };
                    
                    // Add state parameter if present
                    if (context.Properties.Items.TryGetValue(".xsrf", out var state))
                    {
                        queryParams["state"] = state;
                    }
                    
                    var newRedirectUri = Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(authUrl, queryParams);
                    
                    context.Response.Redirect(newRedirectUri);
                },
                
                OnCreatingTicket = async context =>
                {
                    // Get the server and credentials from stored properties
                    var hostname = context.Properties.Items.TryGetValue("mastodon_server", out var storedServer) 
                        ? storedServer 
                        : throw new InvalidOperationException("Mastodon server not found in properties");

                    // Dynamically set endpoints for this specific server
                    var userInfoEndpoint = $"https://{hostname}/api/v1/accounts/verify_credentials";

                    var localDbService = localDbFactory.GetInstance(context.HttpContext);
                    
                    // Use the dynamic user info endpoint
                    var request = new HttpRequestMessage(HttpMethod.Get, userInfoEndpoint);
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.AccessToken);
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    var response = await context.Backchannel.SendAsync(request, context.HttpContext.RequestAborted);
                    response.EnsureSuccessStatusCode();

                    if (context.Options.SaveTokens)
                    {
                        context.Properties.StoreTokens(new[] {
                            new AuthenticationToken { Name = "access_token", Value = context.AccessToken }
                        });
                    }

                    using var user = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                    context.RunClaimActions(user.RootElement);

                    var username = context.Identity?.FindFirst(ClaimTypes.Name)?.Value;

                    // Check if this user is an admin for any Mastodon server
                    var isAdmin = adminConfig?.AdminUsers?.Any(a =>
                        a.Type.Equals("Mastodon", StringComparison.OrdinalIgnoreCase) &&
                        a.Id == username) ?? false;

                    Console.WriteLine($"Dynamic Mastodon login - Server: {hostname}, User: {username}, Is admin: {isAdmin}");

                    var invitationCode = context.Properties.Items.ContainsKey("invitationCode")
                        ? context.Properties.Items["invitationCode"]
                        : context.HttpContext.Request.Cookies["invitationCode"];

                    if (isAdmin)
                    {
                        context.Principal.AddIdentity(new ClaimsIdentity(new[] {
                            new Claim("urn:mastodon:hostname", hostname),
                            new Claim(ClaimTypes.Role, "admin"),
                            new Claim("urn:badgefed:group", "system")
                        }));
                    }
                    else
                    {
                        var userId = $"{hostname}_{username}";
                        
                        try
                        {
                            var registeredUser = localDbService.GetUserById(userId);

                            if (!string.IsNullOrEmpty(invitationCode) && registeredUser == null)
                            {
                                var invitationService = context.HttpContext.RequestServices.GetRequiredService<InvitationService>();
                                var invitation = invitationService.ValidateAndGetInvitation(invitationCode);

                                if (invitation != null)
                                {
                                    registeredUser = new User
                                    {
                                        Id = userId,
                                        Email = invitation.Email,
                                        GivenName = username ?? "User",
                                        Surname = "",
                                        CreatedAt = DateTime.UtcNow,
                                        Provider = "Mastodon",
                                        Role = invitation.Role,
                                        GroupId = invitation.GroupId,
                                        IsActive = true
                                    };

                                    invitationService.AcceptInvitation(invitationCode, registeredUser);
                                    Console.WriteLine($"User created from invitation: {userId} with role {invitation.Role} and group {invitation.GroupId}");
                                }
                            }

                            var role = registeredUser != null ? registeredUser.Role : "user";

                            context.Principal.AddIdentity(new ClaimsIdentity(new[] {
                                new Claim("urn:mastodon:hostname", hostname),
                                new Claim(ClaimTypes.Role, role),
                                new Claim("urn:badgefed:group", registeredUser?.GroupId ?? "system")
                            }));
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error processing user {userId}: {ex.Message}");
                            throw;
                        }
                    }
                },
                
                OnRemoteFailure = context =>
                {
                    Console.WriteLine($"Dynamic Mastodon OAuth failure - Error: {context.Failure}");
                    context.HandleResponse();
                    context.Response.Redirect("/admin/denied");
                    return Task.FromResult(0);
                }
            };

            configureOptions(o);
        });
    }

    // Helper method to get or register Mastodon OAuth app for a specific server
    private static async Task<(string clientId, string clientSecret)> GetOrRegisterMastodonAppAsync(
        string hostname, 
        IServiceProvider services)
    {
        // Check cache first
        if (_mastodonAppCache.TryGetValue(hostname, out var cached))
        {
            return cached;
        }

        // Use semaphore to prevent multiple registrations for the same server
        await _registrationSemaphore.WaitAsync();
        try
        {
            // Double-check after acquiring lock
            if (_mastodonAppCache.TryGetValue(hostname, out cached))
            {
                return cached;
            }

            Console.WriteLine($"Registering new Mastodon OAuth app for {hostname}...");
            
            var registrationService = services.GetRequiredService<MastodonRegistrationService>();
            var appDoc = await registrationService.RegisterApplicationAsync(hostname);
            
            var clientId = appDoc.RootElement.GetProperty("client_id").GetString();
            var clientSecret = appDoc.RootElement.GetProperty("client_secret").GetString();
            
            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            {
                throw new InvalidOperationException($"Failed to register Mastodon app for {hostname}: missing credentials");
            }
            
            // Cache the credentials
            var credentials = (clientId, clientSecret);
            _mastodonAppCache[hostname] = credentials;
            
            Console.WriteLine($"Successfully registered Mastodon OAuth app for {hostname}");
            return credentials;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error registering Mastodon app for {hostname}: {ex.Message}");
            throw;
        }
        finally
        {
            _registrationSemaphore.Release();
        }
    }

    public static AuthenticationBuilder AddMastodon(
        this AuthenticationBuilder builder,
        AdminConfig adminConfig,
        MastodonConfig mastodonConfig,
        Action<OAuthOptions> configureOptions,
        LocalDbFactory localDbFactory)
    {
        var hostname = mastodonConfig.Server;
        _hosts.Add(hostname);
        return builder.AddOAuth(hostname, hostname, o =>
        {
            if (string.IsNullOrEmpty(hostname) || Uri.CheckHostName(hostname) == UriHostNameType.Unknown)
            {
                throw new ArgumentException("Invalid hostname", nameof(hostname));
            }

            o.AuthorizationEndpoint = $"https://{hostname}/oauth/authorize";
            o.TokenEndpoint = $"https://{hostname}/oauth/token";
            o.UserInformationEndpoint = $"https://{hostname}/api/v1/accounts/verify_credentials";
            o.CallbackPath = new Microsoft.AspNetCore.Http.PathString($"/signin-mastodon-{hostname}");

            o.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
            o.ClaimActions.MapJsonKey(ClaimTypes.Name, "username");
            o.ClaimActions.MapJsonKey($"urn:mastodon:id", "id");
            o.ClaimActions.MapJsonKey($"urn:mastodon:username", "username");

            o.Events = new OAuthEvents
            {
                OnCreatingTicket = async context =>
                {
                    var localDbService = localDbFactory.GetInstance(context.HttpContext);
                    var request = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.AccessToken);
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    var response = await context.Backchannel.SendAsync(request, context.HttpContext.RequestAborted);
                    response.EnsureSuccessStatusCode();

                    if (context.Options.SaveTokens)
                    {
                        context.Properties.StoreTokens(new[] {
                            new AuthenticationToken { Name = "access_token", Value = context.AccessToken }
                        });
                    }

                    using var user = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

                    context.RunClaimActions(user.RootElement);

                    // Now that we have the user information, check if they're an admin
                    var username = context.Identity?.FindFirst(ClaimTypes.Name)?.Value;

                    // Check if this user is in the admin list
                    var isAdmin = adminConfig?.AdminUsers?.Any(a =>
                        a.Type.Equals("Mastodon", StringComparison.OrdinalIgnoreCase) &&
                        a.Id == username) ?? false;

                    Console.WriteLine($"Is admin: {username} {isAdmin}");

                    // Check for invitation code
                    var invitationCode = context.Properties.Items.ContainsKey("invitationCode")
                        ? context.Properties.Items["invitationCode"]
                        : context.HttpContext.Request.Cookies["invitationCode"];

                    Console.WriteLine($"Invitation code: {(invitationCode != null ? invitationCode : "<none>")}");

                    if (isAdmin)
                    {
                        context.Principal.AddIdentity(new ClaimsIdentity(new[] {
                            new Claim("urn:mastodon:hostname", hostname),
                            new Claim(ClaimTypes.Role, "admin"),
                            new Claim("urn:badgefed:group", "system")
                        }));
                    }
                    else
                    {
                        var userId = $"{hostname}_{username}";
                        Console.WriteLine($"User ID: {userId} [ic: {invitationCode}]");

                        try
                        {
                            var registeredUser = localDbService.GetUserById(userId);

                            if (registeredUser != null)
                            {
                                Console.WriteLine($"User {userId} is already registered.");
                            }
                            else
                            {
                                Console.WriteLine($"User {userId} is not registered.");
                            }

                            // Handle invitation if provided
                            if (!string.IsNullOrEmpty(invitationCode) && registeredUser == null)
                            {
                                var invitationService = context.HttpContext.RequestServices.GetRequiredService<InvitationService>();
                                var invitation = invitationService.ValidateAndGetInvitation(invitationCode);

                                if (invitation != null)
                                {
                                    // Create new user from invitation
                                    registeredUser = new User
                                    {
                                        Id = userId,
                                        Email = invitation.Email,
                                        GivenName = username ?? "User",
                                        Surname = "",
                                        CreatedAt = DateTime.UtcNow,
                                        Provider = "Mastodon",
                                        Role = invitation.Role,
                                        GroupId = invitation.GroupId,
                                        IsActive = true
                                    };

                                    invitationService.AcceptInvitation(invitationCode, registeredUser);
                                    Console.WriteLine($"User created from invitation: {userId} with role {invitation.Role} and group {invitation.GroupId}");
                                }
                                else
                                {
                                    Console.WriteLine($"Invitation code {invitationCode} is invalid or expired.");
                                }
                            }

                            var role = registeredUser != null ? registeredUser.Role : "user";

                            context.Principal.AddIdentity(new ClaimsIdentity(new[] {
                                new Claim("urn:mastodon:hostname", hostname),
                                new Claim(ClaimTypes.Role, role),
                                new Claim("urn:badgefed:group", registeredUser?.GroupId ?? "system")
                            }));
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error processing user {userId}: {ex.Message}");
                            throw;
                        }
                    }
                },
                OnRemoteFailure = context =>
                {
                    context.HandleResponse();
                    context.Response.Redirect("/admin/denied");
                    return Task.FromResult(0);
                }
            };

            configureOptions(o);
        });
    }
}

public static class LinkedInOAuthExtensions
{
    public static AuthenticationBuilder AddLinkedIn(
        this AuthenticationBuilder builder,
        AdminConfig adminConfig,
        LinkedInConfig config,
        Action<Microsoft.AspNetCore.Authentication.OAuth.OAuthOptions> configureOptions,
        LocalDbFactory localDbFactory)
    {
        return builder.AddOAuth("LinkedIn", "LinkedIn", o =>
        {
            o.AuthorizationEndpoint = "https://www.linkedin.com/oauth/v2/authorization";
            o.TokenEndpoint = "https://www.linkedin.com/oauth/v2/accessToken";
            o.UserInformationEndpoint = "https://api.linkedin.com/v2/userinfo";
            o.ClientId = config.ClientId;
            o.ClientSecret = config.ClientSecret;
            o.CallbackPath = "/signin-linkedin";
            o.Scope.Add("openid");
            o.Scope.Add("email");
            o.Scope.Add("profile");
            o.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
            o.Events = new Microsoft.AspNetCore.Authentication.OAuth.OAuthEvents
            {
                OnCreatingTicket = async context =>
                {
                    var localDbService = localDbFactory.GetInstance(context.HttpContext);

                    Console.WriteLine($"LinkedIn access token: {context.AccessToken}");
                    var request = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.AccessToken);
                    var response = await context.Backchannel.SendAsync(request);
                    response.EnsureSuccessStatusCode(); // Ensure we got a successful response

                    using var userJson = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                    context.RunClaimActions(userJson.RootElement);

                    Console.WriteLine(JsonSerializer.Serialize(userJson.RootElement));

                    // Extract email and name directly from userJson.RootElement
                    var userEmail = userJson.RootElement.GetProperty("email").GetString();
                    var name = userJson.RootElement.GetProperty("name").GetString();

                    Console.WriteLine($"LinkedIn user: {name} {userEmail}");

                    // Check if this user is in the admin list
                    var isAdmin = adminConfig?.AdminUsers?.Any(a => 
                        a.Type.Equals("LinkedIn", StringComparison.OrdinalIgnoreCase) && 
                        a.Id == userEmail) ?? false;

                    Console.WriteLine($"Is admin: {isAdmin}");

                    var role = "user";
                    
                    // suppor OAuth state, and also cookies
                    var invitationCode = context.Properties.Items.ContainsKey("invitationCode")
                        ? context.Properties.Items["invitationCode"]
                        : context.HttpContext.Request.Cookies["invitationCode"];

                    var groupId = "system";

                    if (isAdmin)
                    {
                        role = "admin";
                    }
                    else
                    {
                        var registeredUserId = "LinkedIn_" + userEmail;
                        var registeredUser = localDbService.GetUserById(registeredUserId);

                        // Handle invitation if provided
                        if (!string.IsNullOrEmpty(invitationCode) && registeredUser == null)
                        {
                            var invitationService = context.HttpContext.RequestServices.GetRequiredService<InvitationService>();
                            var invitation = invitationService.ValidateAndGetInvitation(invitationCode);

                            if (invitation != null)
                            {
                                // Create new user from invitation
                                registeredUser = new User
                                {
                                    Id = registeredUserId,
                                    Email = userEmail ?? invitation.Email,
                                    GivenName = name ?? "User",
                                    Surname = "",
                                    CreatedAt = DateTime.UtcNow,
                                    Provider = "LinkedIn",
                                    Role = invitation.Role,
                                    GroupId = invitation.GroupId,
                                    IsActive = true
                                };

                                invitationService.AcceptInvitation(invitationCode, registeredUser);
                                Console.WriteLine($"User created from invitation: {registeredUserId} with role {invitation.Role} and group {invitation.GroupId}");
                            }
                        }
                        else if (registeredUser == null)
                        {
                            registeredUser = new User
                            {
                                Id = registeredUserId,
                                Email = userEmail ?? string.Empty,
                                GivenName = name ?? "User",
                                Surname = string.Empty,
                                CreatedAt = DateTime.UtcNow,
                                Provider = "LinkedIn",
                                Role = "user",
                                IsActive = false
                            };

                            localDbService.UpsertUser(registeredUser);
                        }

                        role = registeredUser != null ? registeredUser.Role : "user";
                        groupId = registeredUser != null ? registeredUser.GroupId : "system";
                    }

                    var userName = context.Identity?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Guid.NewGuid().ToString();

                    Console.WriteLine($"User {name} {userName} {userEmail} is an {role}");

                    context.Principal.AddIdentity(new ClaimsIdentity(
                        [
                            new Claim(ClaimTypes.Name, name ?? userName),
                            new Claim(ClaimTypes.Role, role),
                            new Claim(ClaimTypes.NameIdentifier, userName),
                            new Claim(ClaimTypes.Email, userEmail ?? string.Empty),
                            new Claim("urn:badgefed:group", groupId)
                        ], "LinkedIn"));
                },
                OnRemoteFailure = ctx =>
                {
                    Console.WriteLine($"Remote failure: {ctx.Failure}");
                    ctx.HandleResponse();
                    ctx.Response.Redirect("/admin/denied");
                    return Task.CompletedTask;
                }
            };
            configureOptions(o);
        });
    }
}

public static class GoogleOAuthExtensions
{
    public static AuthenticationBuilder AddGoogle(
        this AuthenticationBuilder builder,
        AdminConfig adminConfig,
        GoogleConfig config,
        Action<Microsoft.AspNetCore.Authentication.OAuth.OAuthOptions> configureOptions,
        LocalDbFactory localDbFactory)
    {
        return builder.AddOAuth("Google", "Google", o =>
        {
            o.AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
            o.TokenEndpoint = "https://oauth2.googleapis.com/token";
            o.UserInformationEndpoint = "https://www.googleapis.com/oauth2/v2/userinfo";
            o.ClientId = config.ClientId;
            o.ClientSecret = config.ClientSecret;
            o.CallbackPath = "/signin-google";
            o.Scope.Add("openid");
            o.Scope.Add("email");
            o.Scope.Add("profile");
            o.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
            o.ClaimActions.MapJsonKey(ClaimTypes.Name, "name");
            o.ClaimActions.MapJsonKey(ClaimTypes.Email, "email");
            o.ClaimActions.MapJsonKey(ClaimTypes.GivenName, "given_name");
            o.ClaimActions.MapJsonKey(ClaimTypes.Surname, "family_name");
            o.ClaimActions.MapJsonKey("urn:google:profile", "link");
            o.ClaimActions.MapJsonKey("urn:google:image", "picture");
            o.Events = new Microsoft.AspNetCore.Authentication.OAuth.OAuthEvents
            {
                OnCreatingTicket = async context =>
                {
                    var localDbService = localDbFactory.GetInstance(context.HttpContext);
                    Console.WriteLine($"Google access token: {context.AccessToken}");
                    var request = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.AccessToken);
                    var response = await context.Backchannel.SendAsync(request);
                    response.EnsureSuccessStatusCode(); // Ensure we got a successful response

                    using var userJson = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                    context.RunClaimActions(userJson.RootElement);

                    Console.WriteLine(JsonSerializer.Serialize(userJson.RootElement));

                    // Extract email and name directly from userJson.RootElement
                    var userEmail = userJson.RootElement.GetProperty("email").GetString();
                    var name = userJson.RootElement.GetProperty("name").GetString();
                    var givenName = userJson.RootElement.TryGetProperty("given_name", out var gnElement) ? gnElement.GetString() : "";
                    var familyName = userJson.RootElement.TryGetProperty("family_name", out var fnElement) ? fnElement.GetString() : "";

                    Console.WriteLine($"Google user: {name} {userEmail}");

                    // Check if this user is in the admin list
                    var isAdmin = adminConfig?.AdminUsers?.Any(a => 
                        a.Type.Equals("Google", StringComparison.OrdinalIgnoreCase) && 
                        a.Id == userEmail) ?? false;

                    Console.WriteLine($"Is admin: {isAdmin}");

                    var role = "user";

                    // Check for invitation code
                    var invitationCode = context.Properties.Items.ContainsKey("invitationCode") 
                        ? context.Properties.Items["invitationCode"] 
                        : null;

                    if (isAdmin)
                    {
                        role = "admin";
                    }
                    else
                    {
                        var registeredUserId = "Google_" + userEmail;
                        var registeredUser = localDbService.GetUserById(registeredUserId);

                        // Handle invitation if provided
                        if (!string.IsNullOrEmpty(invitationCode) && registeredUser == null)
                        {
                            var invitationService = context.HttpContext.RequestServices.GetRequiredService<InvitationService>();
                            var invitation = invitationService.ValidateAndGetInvitation(invitationCode);
                            
                            if (invitation != null)
                            {
                                // Create new user from invitation
                                registeredUser = new User
                                {
                                    Id = registeredUserId,
                                    Email = userEmail ?? invitation.Email,
                                    GivenName = givenName ?? name ?? "User",
                                    Surname = familyName ?? "",
                                    CreatedAt = DateTime.UtcNow,
                                    Provider = "Google",
                                    Role = invitation.Role,
                                    GroupId = invitation.GroupId,
                                    IsActive = true
                                };
                                
                                invitationService.AcceptInvitation(invitationCode, registeredUser);
                                Console.WriteLine($"User created from invitation: {registeredUserId} with role {invitation.Role} and group {invitation.GroupId}");
                            }
                        }
                        else if (registeredUser == null)
                        {
                            registeredUser = new User
                            {
                                Id = registeredUserId,
                                Email = userEmail ?? string.Empty,
                                GivenName = givenName ?? name ?? "User",
                                Surname = familyName ?? string.Empty,
                                CreatedAt = DateTime.UtcNow,
                                Provider = "Google",
                                Role = "user",
                                IsActive = false
                            };

                            localDbService.UpsertUser(registeredUser);
                        }

                        role = registeredUser != null ? registeredUser.Role : "user";
                    }

                    var userName = context.Identity?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Guid.NewGuid().ToString();

                    Console.WriteLine($"User {name} {userName} {userEmail} is an {role}");

                    context.Principal.AddIdentity(new ClaimsIdentity(
                        [
                            new Claim(ClaimTypes.Name, name ?? userName),
                            new Claim(ClaimTypes.Role, role),
                            new Claim(ClaimTypes.NameIdentifier, userName),
                            new Claim(ClaimTypes.Email, userEmail ?? string.Empty),
                            new Claim(ClaimTypes.GivenName, givenName ?? ""),
                            new Claim(ClaimTypes.Surname, familyName ?? ""),
                            new Claim("urn:google:profile", userJson.RootElement.TryGetProperty("link", out var linkElement) ? linkElement.GetString() ?? "" : ""),
                            new Claim("urn:google:image", userJson.RootElement.TryGetProperty("picture", out var pictureElement) ? pictureElement.GetString() ?? "" : ""),
                        ], "Google"));
                },
                OnRemoteFailure = ctx =>
                {
                    Console.WriteLine($"Remote failure: {ctx.Failure}");
                    ctx.HandleResponse();
                    ctx.Response.Redirect("/admin/denied");
                    return Task.CompletedTask;
                }
            };
            configureOptions(o);
        });
    }
}

// Helper classes for config
public class AdminConfig
{
    public List<AdminUser> AdminUsers { get; set; }
}

public class AdminUser
{
    public string Id { get; set; }
    public string Type { get; set; }
}

public class LinkedInConfig
{
    public string ClientId { get; set; }
    public string ClientSecret { get; set; }
}

public class GoogleConfig
{
    public string ClientId { get; set; }
    public string ClientSecret { get; set; }
}

public class MastodonConfig
{
    public string ClientId { get; set; }
    public string ClientSecret { get; set; }
    public string Server { get; set; }
}

public class GotoSocialConfig
{
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string Server { get; set; }
}