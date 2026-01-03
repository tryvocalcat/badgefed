using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace BadgeFed.Controllers
{
    public class LoginController : Controller
    {
        private readonly ILogger<LoginController> _logger;

        public LoginController(ILogger<LoginController> logger)
        {
            _logger = logger;
        }
        [HttpGet]
        [Route("/admin/auth/mastodon")]
        public IActionResult LoginWithMastodon(string returnUrl = "/admin", string? invitationCode = null)
        {
            _logger.LogInformation("[{RequestHost}] Initiating Mastodon login with return URL: {ReturnUrl}, invitation code: {HasInvitationCode}", Request.Host, returnUrl, !string.IsNullOrEmpty(invitationCode));
            
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
            _logger.LogInformation("[{RequestHost}] Initiating GotoSocial login with return URL: {ReturnUrl}, invitation code: {HasInvitationCode}", Request.Host, returnUrl, !string.IsNullOrEmpty(invitationCode));
            
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
            _logger.LogInformation("[{RequestHost}] Initiating LinkedIn login with return URL: {ReturnUrl}, invitation code: {HasInvitationCode}", Request.Host, returnUrl, !string.IsNullOrEmpty(invitationCode));
            
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
            _logger.LogInformation("[{RequestHost}] Initiating Google login with return URL: {ReturnUrl}, invitation code: {HasInvitationCode}", Request.Host, returnUrl, !string.IsNullOrEmpty(invitationCode));
            
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
            _logger.LogInformation("[{RequestHost}] User logging out", Request.Host);
            await HttpContext.SignOutAsync();
            _logger.LogInformation("[{RequestHost}] User successfully logged out", Request.Host);
            return Redirect("/");
        }
    }
}
