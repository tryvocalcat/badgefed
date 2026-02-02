using BadgeFed.Services;
using Microsoft.AspNetCore.Mvc;

namespace BadgeFed.Controllers
{
    [Route(".well-known")]
    [ApiController]
    public class WebFingerController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly LocalScopedDb _localDbService;
        private readonly ILogger<WebFingerController> _logger;

        public WebFingerController(IConfiguration configuration, LocalScopedDb localDbService, ILogger<WebFingerController> logger)
        {
            _configuration = configuration;
            _localDbService = localDbService;
            _logger = logger;
        }

        [HttpGet("webfinger")]
        public IActionResult GetWebFinger([FromQuery] string resource)
        {
            _logger.LogInformation("[{RequestHost}] WebFinger request for resource: {Resource}", Request.Host, resource);
            
            if (string.IsNullOrEmpty(resource) || !resource.StartsWith("acct:"))
            {
                _logger.LogWarning("[{RequestHost}] Invalid WebFinger resource parameter: {Resource}", Request.Host, resource);
                return BadRequest("Invalid resource parameter");
            }

            var account = resource.Substring("acct:".Length);
            var domain = account.Split('@')[1];
            var actorName = account.Split('@')[0];

            var actor = _localDbService.GetActorByFilter($"Username = \"{actorName}\" AND Domain = \"{domain}\"");

            if (actor == null)
            {
                _logger.LogWarning("[{RequestHost}] Account not found for WebFinger request: {Account}", Request.Host, account);
                return NotFound("Account not found on this domain");
            }
            
            _logger.LogInformation("[{RequestHost}] Successfully processed WebFinger request for: {Account}", Request.Host, account);

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