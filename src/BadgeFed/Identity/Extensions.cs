using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Mvc;

namespace Microsoft.AspNetCore.Routing;

internal static class LoginLogoutEndpointRouteBuilderExtensions
{
    internal static IEndpointConventionBuilder MapLoginAndLogout(this IEndpointRouteBuilder endpoints, List<string>? servers = null)
    {
        var group = endpoints.MapGroup("");

        // Add dynamic Mastodon OAuth route that accepts any server
        group.MapGet($"/login/oauth/{{server}}", (string server, string? returnUrl, string? invitationCode) =>
        {
            Console.WriteLine(returnUrl);
            var authProps = GetAuthProperties(returnUrl);

            if (!string.IsNullOrEmpty(invitationCode))
            {
                authProps.Items["invitationCode"] = invitationCode;
            }

            // Store the server in the authentication properties
            authProps.Items["mastodon_server"] = server;

            // Use the DynamicMastodon scheme
            return TypedResults.Challenge(authProps, new[] { "DynamicMastodon" });
        }).AllowAnonymous();

        // Keep the legacy server-specific routes for backward compatibility if needed
        if (servers?.Any() ?? false)
        {
            foreach (var server in servers)
            {
                group.MapGet($"/login/legacy/{server}", (string? returnUrl, string? invitationCode) =>
                {
                    var authProps = GetAuthProperties(returnUrl);

                    if (!string.IsNullOrEmpty(invitationCode))
                    {
                        authProps.Items["invitationCode"] = invitationCode;
                    }

                    authProps.Items["mastodon_server"] = server;
                    return TypedResults.Challenge(authProps, new[] { "DynamicMastodon" });
                }).AllowAnonymous();
            }
        }

        // Sign out of the Cookie and OIDC handlers. If you do not sign out with the OIDC handler,
        // the user will automatically be signed back in the next time they visit a page that requires authentication
        // without being able to choose another account.
        group.MapGet("/logout", async (string? returnUrl) =>
        {
            // First sign out of the cookie auth scheme
            Console.WriteLine($"GET /logout called with returnUrl: {returnUrl}");

            return TypedResults.SignOut(
                GetAuthProperties(returnUrl),
                [
                    CookieAuthenticationDefaults.AuthenticationScheme
                ]);
        });

        group.MapPost("/logout", (HttpContext httpContext, [FromForm] string? returnUrl) => 
        {
            Console.WriteLine("POST /logout called");
            Console.WriteLine($"Request Content-Type: {httpContext.Request.ContentType}");
            Console.WriteLine($"Form returnUrl: {returnUrl}");
            Console.WriteLine($"Query returnUrl: {httpContext.Request.Query["returnUrl"]}");
            Console.WriteLine($"Form count: {httpContext.Request.Form.Count}");
            
            foreach (var form in httpContext.Request.Form)
            {
            Console.WriteLine($"Form key: {form.Key}, value: {form.Value}");
            }
            
            try
            {
            var authProps = GetAuthProperties(returnUrl);
          
            return TypedResults.SignOut(authProps,
                [
                CookieAuthenticationDefaults.AuthenticationScheme
                ]);
            }
            catch (Exception ex)
            {
            Console.WriteLine($"Exception in logout: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            throw;
            }
        });

        return group;
    }

    private static AuthenticationProperties GetAuthProperties(string? returnUrl)
    {
        Console.WriteLine($"GetAuthProperties called with returnUrl: {returnUrl}");
        // TODO: Use HttpContext.Request.PathBase instead.
        const string pathBase = "/";

        // Prevent open redirects.
        if (string.IsNullOrEmpty(returnUrl))
        {
            Console.WriteLine("returnUrl is null or empty, setting to pathBase");
            returnUrl = pathBase;
        }
        else if (!Uri.IsWellFormedUriString(returnUrl, UriKind.Relative))
        {
            Console.WriteLine($"returnUrl is not a relative URI: {returnUrl}");
            returnUrl = new Uri(returnUrl, UriKind.Absolute).PathAndQuery;
        }
        else if (returnUrl[0] != '/')
        {
            Console.WriteLine($"returnUrl does not start with '/': {returnUrl}");
            returnUrl = $"{pathBase}{returnUrl}";
        }

        Console.WriteLine($"Final returnUrl after processing: {returnUrl}");

        return new AuthenticationProperties { RedirectUri = returnUrl };
    }
}