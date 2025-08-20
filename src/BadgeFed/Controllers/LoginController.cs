using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace BadgeFed.Controllers
{
    public class LoginController : Controller
    {
        [HttpGet]
        [Route("/admin/auth/mastodon")]
        public IActionResult LoginWithMastodon(string returnUrl = "/admin", string? invitationCode = null)
        {
            var hostname = User.FindFirst("urn:mastodon:hostname")?.Value;
            if (string.IsNullOrEmpty(hostname))
            {
                var config = HttpContext.RequestServices.GetRequiredService<MastodonConfig>();
                hostname = config.Server;
            }
            
            var properties = new AuthenticationProperties
            {
                RedirectUri = returnUrl
            };
            
            // Pass invitation code through authentication properties
            if (!string.IsNullOrEmpty(invitationCode))
            {
                properties.Items["invitationCode"] = invitationCode;
            }
            
            return Challenge(properties, hostname);
        }

        [HttpGet]
        [Route("/admin/auth/gotosocial")]
        public IActionResult LoginWithGotoSocial(string returnUrl = "/admin", string? invitationCode = null)
        {
            var hostname = User.FindFirst("urn:gotosocial:hostname")?.Value;
            if (string.IsNullOrEmpty(hostname))
            {
                var config = HttpContext.RequestServices.GetRequiredService<GotoSocialConfig>();
                hostname = config.Server;
            }

            var properties = new AuthenticationProperties
            {
                RedirectUri = returnUrl
            };

            // Pass invitation code through authentication properties
            if (!string.IsNullOrEmpty(invitationCode))
            {
                properties.Items["invitationCode"] = invitationCode;
            }

            return Challenge(properties, hostname);
        }

        [HttpGet]
        [Route("/admin/login/linkedin")]
        public IActionResult LoginWithLinkedIn(string returnUrl = "/admin", string? invitationCode = null)
        {
            var properties = new AuthenticationProperties
            {
                RedirectUri = returnUrl
            };
            
            // Pass invitation code through authentication properties
            if (!string.IsNullOrEmpty(invitationCode))
            {
                properties.Items["invitationCode"] = invitationCode;
            }
            
            return Challenge(properties, "LinkedIn");
        }

        [HttpGet]
        [Route("/admin/login/google")]
        public IActionResult LoginWithGoogle(string returnUrl = "/admin", string? invitationCode = null)
        {
            var properties = new AuthenticationProperties
            {
                RedirectUri = returnUrl
            };
            
            // Pass invitation code through authentication properties
            if (!string.IsNullOrEmpty(invitationCode))
            {
                properties.Items["invitationCode"] = invitationCode;
            }
            
            return Challenge(properties, "Google");
        }

        [HttpGet]
        [Route("/admin/auth/logout")]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync();
            return Redirect("/");
        }
    }
}
