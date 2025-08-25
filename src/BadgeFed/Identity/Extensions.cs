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

        if (servers?.Any() ?? false)
        {
            foreach (var server in servers)
            {
                group.MapGet($"/login/oauth/{server}", (string? returnUrl, string? invitationCode) =>
                {
                    var authProps = GetAuthProperties(returnUrl);

                    if (!string.IsNullOrEmpty(invitationCode))
                    {
                        authProps.Items["invitationCode"] = invitationCode;
                    }

                    return TypedResults.Challenge(authProps, new[] { server });
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
        // TODO: Use HttpContext.Request.PathBase instead.
        const string pathBase = "/";

        // Prevent open redirects.
        if (string.IsNullOrEmpty(returnUrl))
        {
            returnUrl = pathBase;
        }
        else if (!Uri.IsWellFormedUriString(returnUrl, UriKind.Relative))
        {
            returnUrl = new Uri(returnUrl, UriKind.Absolute).PathAndQuery;
        }
        else if (returnUrl[0] != '/')
        {
            returnUrl = $"{pathBase}{returnUrl}";
        }

        return new AuthenticationProperties { RedirectUri = returnUrl };
    }
}