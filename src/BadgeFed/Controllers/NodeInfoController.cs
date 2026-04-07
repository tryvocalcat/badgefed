using BadgeFed.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Reflection;

namespace BadgeFed.Controllers
{
    [Route("nodeinfo")]
    [ApiController]
    public class NodeInfoController : ControllerBase
    {
        private readonly LocalScopedDb _localDbService;
        private readonly OpenRegistrationService _openRegistrationService;
        private readonly IMemoryCache _cache;
        private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(48);

        public NodeInfoController(LocalScopedDb localDbService, OpenRegistrationService openRegistrationService, IMemoryCache cache)
        {
            _localDbService = localDbService;
            _openRegistrationService = openRegistrationService;
            _cache = cache;
        }

        [HttpGet("2.1")]
        [ResponseCache(Duration = 172800)]
        public IActionResult GetNodeInfo()
        {
            var cacheKey = $"nodeinfo_2.1_{Request.Host.Value}";

            if (_cache.TryGetValue(cacheKey, out object? cached))
            {
                return new JsonResult(cached)
                {
                    ContentType = "application/json; profile=\"http://nodeinfo.diaspora.software/ns/schema/2.1#\""
                };
            }

            var version = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.1";

            var actors = _localDbService.GetActors();
            var totalUsers = actors.Count;

            var instanceDescription = _localDbService.GetInstanceDescription();
            var instanceStats = _localDbService.GetInstanceStats();

            var mainActor = _localDbService.GetMainActor();
            var domain = Request.Host.Value;
            var staffAccounts = mainActor != null
                ? new[] { $"https://{mainActor.Domain}/actors/{mainActor.Domain}/{mainActor.Username}" }
                : Array.Empty<string>();

            var nodeInfo = new
            {
                version = "2.1",
                software = new
                {
                    name = "badgefed",
                    version = version,
                    repository = "https://github.com/tryvocalcat/badgefed",
                    homepage = "https://badgefed.org"
                },
                protocols = new[] { "activitypub" },
                services = new
                {
                    outbound = Array.Empty<string>(),
                    inbound = Array.Empty<string>()
                },
                usage = new
                {
                    users = new
                    {
                        total = totalUsers,
                        activeMonth = totalUsers,
                        activeHalfyear = totalUsers
                    },
                    localPosts = instanceStats.IssuedCount - instanceStats.ExternalBadgesCount
                },
                openRegistrations = _openRegistrationService.IsEnabled(),
                metadata = new
                {
                    nodeName = !string.IsNullOrEmpty(instanceDescription.Name) ? instanceDescription.Name : "BadgeFed",
                    nodeDescription = !string.IsNullOrEmpty(instanceDescription.Description) ? instanceDescription.Description : "",
                    staffAccounts,
                    federation = new { enabled = true }
                }
            };

            _cache.Set(cacheKey, nodeInfo, CacheDuration);

            return new JsonResult(nodeInfo)
            {
                ContentType = "application/json; profile=\"http://nodeinfo.diaspora.software/ns/schema/2.1#\""
            };
        }
    }
}
