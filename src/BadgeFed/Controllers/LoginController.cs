using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace BadgeFed.Controllers
{
    public class LoginController : Controller
    {
        [HttpGet]
        [Route("/admin/auth/mastodon")]
        public IActionResult LoginWithMastodon(string returnUrl = "/admin")
        {
            var hostname = User.FindFirst("urn:mastodon:hostname")?.Value;
            if (string.IsNullOrEmpty(hostname))
            {
                var config = HttpContext.RequestServices.GetRequiredService<MastodonConfig>();
                hostname = config.Server;
            }
            
            return Challenge(new AuthenticationProperties
            {
                RedirectUri = returnUrl
            }, hostname);
        }

        [HttpGet]
        [Route("/admin/login/linkedin")]
        public IActionResult LoginWithLinkedIn(string returnUrl = "/admin")
        {
            return Challenge(new AuthenticationProperties
            {
                RedirectUri = returnUrl
            }, "LinkedIn");
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
