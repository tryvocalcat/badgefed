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

builder.Services.AddHttpClient();

builder.Services.AddRazorPages();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddServerSideBlazor();
builder.Services.AddControllers();
builder.Services.AddScoped<FollowService>();
builder.Services.AddScoped<RepliesService>();
builder.Services.Configure<ServerConfig>(builder.Configuration.GetSection("Server"));

var adminConfig = builder.Configuration.GetSection("AdminAuthentication").Get<AdminConfig>();
builder.Services.AddSingleton<AdminConfig>(adminConfig);

builder.Services.AddSingleton<LocalDbService>(sp => {
    var dbFileName = Environment.GetEnvironmentVariable("SQLITE_DB_FILENAME") ?? "test.db";
    return new LocalDbService(dbFileName);
});

builder.Services.AddSingleton<BadgeProcessor>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentUser>();
builder.Services.AddHttpClient();

builder.Services.AddSingleton(sp => new BadgeService(sp.GetRequiredService<LocalDbService>()));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
.AddCookie(option =>
{
    option.LoginPath = "/admin/login/oauth";
    option.LogoutPath = "/admin/logout";
    option.AccessDeniedPath = "/admin/denied";
}).AddMastodon(adminConfig, o => {
    o.Scope.Add("read:statuses");
    o.Scope.Add("read:accounts");
    o.ClientId = "4AXCy0AcLFHncqGyuAG3XeVDGc-EGsUIa2ILg5Tj4cM";
    o.ClientSecret = "kUflIFubG5wBnJdNqL5jnyQmhrnKEnnuqgjsREHvjFU";
    o.SaveTokens = true;
});

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

if (!app.Environment.IsDevelopment())
{
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseStaticFiles();
app.UseCookiePolicy();
app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapControllers();

app.MapGroup("/admin").MapLoginAndLogout();

app.Run();

public static class MastodonOAuthExtensions {
    private static readonly HashSet<string> _hosts = new();

    public static IEnumerable<string> Hosts => _hosts;

    public static AuthenticationBuilder AddMastodon(this AuthenticationBuilder builder, AdminConfig adminConfig, Action<OAuthOptions> configureOptions) {
        var hostname = adminConfig.Server;
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
                OnCreatingTicket = async context => {
                    
                    var request = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.AccessToken);
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    var response = await context.Backchannel.SendAsync(request, context.HttpContext.RequestAborted);
                    response.EnsureSuccessStatusCode();

                    if (context.Options.SaveTokens) {
                        context.Properties.StoreTokens(new[] {
                            new AuthenticationToken { Name = "access_token", Value = context.AccessToken }
                        });
                    }

                    using var user = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                    
                    context.RunClaimActions(user.RootElement);

                    // Now that we have the user information, check if they're an admin
                    var username = context.Identity?.FindFirst(ClaimTypes.Name)?.Value;

                    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(username));
                    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(adminConfig));
                    
                    if (username != null && adminConfig.Username == username)
                    {
                        context.Principal.AddIdentity(new ClaimsIdentity(new[] { 
                            new Claim("urn:mastodon:hostname", hostname),
                            new Claim(ClaimTypes.Role, "Admin")
                        }));
                    }
                    else
                    {
                        context.Principal.AddIdentity(new ClaimsIdentity(new[] { 
                            new Claim("urn:mastodon:hostname", hostname)
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
