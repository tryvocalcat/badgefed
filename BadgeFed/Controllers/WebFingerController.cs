using BadgeFed.Services;
using Microsoft.AspNetCore.Mvc;

namespace BadgeFed.Controllers
{
    [Route(".well-known")]
    [ApiController]
    public class WebFingerController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        private readonly LocalDbService _localDbService;

        public WebFingerController(IConfiguration configuration, LocalDbService localDbService)
        {
            _configuration = configuration;
            _localDbService = localDbService;
        }

        [HttpGet("webfinger")]
        public IActionResult GetWebFinger([FromQuery] string resource)
        {
            if (string.IsNullOrEmpty(resource) || !resource.StartsWith("acct:"))
            {
                return BadRequest("Invalid resource parameter");
            }

            var account = resource.Substring("acct:".Length);
            var domain = account.Split('@')[1];
            var actorName = account.Split('@')[0];

            var actor = _localDbService.GetActorByFilter($"Username = \"{actorName}\" AND Domain = \"{domain}\"");

            if (actor == null)
            {
                return NotFound("Account not found on this domain");
            }

            var subject = $"acct:{actorName}@{domain}";
            var aliases = new[] { $"https://{domain}/actors/{domain}/{actorName}" };

            var links = new[]
            {
                new
                {
                    rel = "self",
                    type = "application/activity+json",
                    href = $"https://{domain}/actors/{domain}/{actorName}"
                },
                new
                {
                    rel = "http://webfinger.net/rel/profile-page",
                    type = "text/html",
                    href = $"{actor.InformationUri}"
                }
            };

            return new JsonResult(new { subject, aliases, links })
            {
                ContentType = "application/activity+json"
            };
        }
    }
}