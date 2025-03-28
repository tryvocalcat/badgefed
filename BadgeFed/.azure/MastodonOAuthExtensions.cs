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

namespace BadgeFed {
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
}