using Microsoft.AspNetCore.Mvc;

namespace BadgeFed.Controllers
{
    [Route(".well-known")]
    [ApiController]
    public class WebFingerController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public WebFingerController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet("webfinger")]
        public IActionResult GetWebFinger([FromQuery] string resource)
        {
            // Placeholder for handling the request
            var domain = _configuration["Domain"];
            var actorName = _configuration["ActorName"];
            var subject = $"acct:{actorName}@{domain}";
            var aliases = new[] { $"https://{domain}/actors/{actorName}" };
            var links = new[]
            {
                new
                {
                    rel = "self",
                    type = "application/activity+json",
                    href = $"https://{domain}/actors/{actorName}"
                },
                new
                {
                    rel = "http://webfinger.net/rel/profile-page",
                    type = "text/html",
                    href = $"https://{domain}/"
                }
            };
            return Ok(new { subject, aliases, links });
        }
    }
}