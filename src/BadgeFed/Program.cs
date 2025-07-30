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

var dbFileName = Environment.GetEnvironmentVariable("SQLITE_DB_FILENAME") ?? "badgefed.db";
var localDbService = new LocalDbService(dbFileName);

builder.Services.AddSingleton<LocalDbService>(localDbService);

builder.Services.AddScoped<FollowService>();
builder.Services.AddScoped<ExternalBadgeService>();
builder.Services.AddScoped<RepliesService>();
builder.Services.AddScoped<CreateNoteService>();
builder.Services.AddScoped<ServerDiscoveryService>();

var adminConfig = builder.Configuration.GetSection("AdminAuthentication").Get<AdminConfig>();
builder.Services.AddSingleton<AdminConfig>(adminConfig);

builder.Services.AddScoped<DatabaseMigrationService>();

builder.Services.AddSingleton<BadgeProcessor>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentUser>();
builder.Services.AddHttpClient();

builder.Services.AddSingleton<OpenBadgeService>();

builder.Services.AddSingleton<BadgeService>();

builder.Services.AddScoped<InvitationService>();

// Add a new configuration section for LinkedIn OAuth
var linkedInConfig = builder.Configuration.GetSection("LinkedInConfig").Get<LinkedInConfig>();
var googleConfig = builder.Configuration.GetSection("GoogleConfig").Get<GoogleConfig>();
var mastodonConfig = builder.Configuration.GetSection("MastodonConfig").Get<MastodonConfig>();

var auth = builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
.AddCookie(option =>
{
    option.LoginPath = "/admin/login";
    option.LogoutPath = "/admin/logout";
    option.AccessDeniedPath = "/admin/denied";
});

if (mastodonConfig != null)
{
    auth.AddMastodon(adminConfig, mastodonConfig, o => {
        o.Scope.Add("read:statuses");
        o.Scope.Add("read:accounts");
        o.ClientId = mastodonConfig.ClientId;
        o.ClientSecret = mastodonConfig.ClientSecret;
        o.SaveTokens = true;
     }, localDbService);
}

if (linkedInConfig != null)
{
    auth.AddLinkedIn(adminConfig, linkedInConfig, o =>
        {
            o.ClientId = linkedInConfig.ClientId;
            o.ClientSecret = linkedInConfig.ClientSecret;
            o.CallbackPath = "/signin-linkedin";
            o.SaveTokens = true;
         }, localDbService);
}

if (googleConfig != null)
{
    auth.AddGoogle(adminConfig, googleConfig, o =>
        {
            o.ClientId = googleConfig.ClientId;
            o.ClientSecret = googleConfig.ClientSecret;
            o.CallbackPath = "/signin-google";
            o.SaveTokens = true;
         }, localDbService);
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

// Setup default actor on first run
await SetupDefaultActor(app.Services);

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

app.MapGroup("/admin").MapLoginAndLogout(loginSchemas);

app.Run();

async Task SetupDefaultActor(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var localDbService = scope.ServiceProvider.GetRequiredService<LocalDbService>();
    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

    // Check if the default actor exists
    var defaultUsername = "admin";
    var defaultDomain = configuration.GetSection("BadgesDomains").Get<string[]>()?.FirstOrDefault() ?? "example.com";

    var existingActor = localDbService.GetActorByFilter($"Username = \"{defaultUsername}\" AND Domain = \"{defaultDomain}\"");
    if (existingActor == null)
    {
        // Generate public/private key pair
        var keyPair = await CryptoService.GenerateKeyPairAsync();

        // Create the default actor
        var defaultActor = new Actor
        {
            FullName = "Default Admin",
            Username = defaultUsername,
            Domain = defaultDomain,
            Summary = "This is the default admin actor.",
            PublicKeyPem = keyPair.PublicKeyPem,
            PrivateKeyPem = keyPair.PrivateKeyPem,
            InformationUri = $"https://{defaultDomain}/about",
            AvatarPath = "img/defaultavatar.png",
            IsMain = true
        };

        localDbService.UpsertActor(defaultActor);
        Console.WriteLine($"Default actor created with username '{defaultUsername}' and domain '{defaultDomain}'.");
    }
}

public static class MastodonOAuthExtensions {
    private static readonly HashSet<string> _hosts = new();

    public static IEnumerable<string> Hosts => _hosts;

    public static AuthenticationBuilder AddMastodon(
        this AuthenticationBuilder builder, 
        AdminConfig adminConfig, 
        MastodonConfig mastodonConfig, 
        Action<OAuthOptions> configureOptions,
        LocalDbService localDbService)
    {
        var hostname = mastodonConfig.Server;
        _hosts.Add(hostname);
        return builder.AddOAuth(hostname, hostname, o => {
            if (string.IsNullOrEmpty(hostname) || Uri.CheckHostName(hostname) == UriHostNameType.Unknown) {
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

            o.Events = new OAuthEvents {
                OnCreatingTicket = async context =>
                {

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
                        : null;

                    if (isAdmin)
                    {
                        context.Principal.AddIdentity(new ClaimsIdentity(new[] {
                            new Claim("urn:mastodon:hostname", hostname),
                            new Claim(ClaimTypes.Role, "admin")
                        }));
                    }
                    else
                    {
                        var userId = $"{hostname}_{username}";
                        Console.WriteLine($"User ID: {userId}");

                        var registeredUser = localDbService.GetUserById(userId);
                        
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
                                    IsActive = true
                                };
                                
                                invitationService.AcceptInvitation(invitationCode, registeredUser);
                                Console.WriteLine($"User created from invitation: {userId} with role {invitation.Role}");
                            }
                        }

                        var role = registeredUser != null ? registeredUser.Role : "user";

                        context.Principal.AddIdentity(new ClaimsIdentity(new[] {
                            new Claim("urn:mastodon:hostname", hostname),
                            new Claim(ClaimTypes.Role, role)
                        }));
                    }
                },
                OnRemoteFailure = context => {
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
        LocalDbService localDbService)
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
                                    IsActive = true
                                };
                                
                                invitationService.AcceptInvitation(invitationCode, registeredUser);
                                Console.WriteLine($"User created from invitation: {registeredUserId} with role {invitation.Role}");
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
                    }

                    var userName = context.Identity?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Guid.NewGuid().ToString();

                    Console.WriteLine($"User {name} {userName} {userEmail} is an {role}");

                    context.Principal.AddIdentity(new ClaimsIdentity(
                        [
                            new Claim(ClaimTypes.Name, name ?? userName),
                            new Claim(ClaimTypes.Role, role),
                            new Claim(ClaimTypes.NameIdentifier, userName),
                            new Claim(ClaimTypes.Email, userEmail ?? string.Empty),
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
        LocalDbService localDbService)
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
                                    IsActive = true
                                };
                                
                                invitationService.AcceptInvitation(invitationCode, registeredUser);
                                Console.WriteLine($"User created from invitation: {registeredUserId} with role {invitation.Role}");
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
