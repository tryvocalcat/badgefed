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

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddControllers();
builder.Services.AddScoped<FollowService>();
builder.Services.AddScoped<RepliesService>();
builder.Services.Configure<ServerConfig>(builder.Configuration.GetSection("Server"));

builder.Services.AddSingleton<LocalDbService>(new LocalDbService("test.db"));
builder.Services.AddSingleton<BadgeProcessor>();
builder.Services.AddHttpContextAccessor();

builder.Services.AddSingleton(sp => new BadgeService(sp.GetRequiredService<LocalDbService>()));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(option =>
    {
        option.LoginPath = "/admin";
        option.LogoutPath = "/admin/authentication/logout";
        option.AccessDeniedPath = "/";
    }).AddMastodon("hachyderm.io", o => {
    o.Scope.Add("read:statuses");
    o.Scope.Add("read:accounts");
    o.ClientId = "badgefed";
    o.ClientSecret = "onelittlesecret";
    o.SaveTokens = true;
});

builder.Services.AddHostedService<JobExecutor>();
builder.Services.AddScoped<JobProcessor>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseCookiePolicy();

app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public static class MastodonOAuthExtensions {
        private static readonly HashSet<string> _hosts = new();

        public static IEnumerable<string> Hosts => _hosts;

        public static AuthenticationBuilder AddMastodon(this AuthenticationBuilder builder, string hostname, Action<OAuthOptions> configureOptions) {
            _hosts.Add(hostname);
            return builder.AddOAuth(hostname, hostname, o => {
                // https://medium.com/@mauridb/using-oauth2-middleware-with-asp-net-core-2-0-b31ffef58cd0

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
                        context.Principal.AddIdentity(new ClaimsIdentity(new[] { new Claim("urn:mastodon:hostname", hostname) }));

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
                    },
                    OnRemoteFailure = context => {
                        context.HandleResponse();
                        context.Response.Redirect("/Home/Error");
                        return Task.FromResult(0);
                    }
                };

                configureOptions(o);
            });
        }
    }
