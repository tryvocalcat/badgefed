using ActivityPubDotNet.Core;
using BadgeFed.Services;
using Microsoft.AspNetCore.Mvc;

namespace BadgeFed.Controllers
{
    [ApiController]
    [Route("admin/grant")]
    public class AdminBadgeController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly BadgeProcessor _badgeProcessor;

        public AdminBadgeController(IConfiguration configuration, BadgeProcessor badgeProcessor)
        {
            _configuration = configuration;
            _badgeProcessor = badgeProcessor;
        }

        [HttpGet("{id}/broadcast")]
        public async Task<IActionResult> BroadcastBadge(string id)
        {
            var recordId = long.Parse(id);
            
            var record = _badgeProcessor.BroadcastGrant(recordId);

            if (record == null)
            {
                return NotFound("No badges to broadcast");
            }

            return Redirect("/admin/grants");
        }

        [HttpGet("{id}/notify")]
        public async Task<IActionResult> NotifyAcceptLink(string id)
        {
            var recordId = long.Parse(id);

            var record = _badgeProcessor.NotifyGrant(recordId);

            if (record == null)
            {
                return NotFound("No badges to notify");
            }

            return Redirect("/admin/grants");
        }

        /** Process signs and create a badgenote **/
        [HttpGet("{id}/process")]
        public async Task<IActionResult> ProcessBadge(string id)
        {
            var recordId = long.Parse(id);
            
            var record = _badgeProcessor.SignAndGenerateBadge(recordId);
            
            if (record == null)
            {
                return NotFound("No badges to process");
            }

            // Redirect to the grants administration page after processing
            return Redirect("/admin/grants");
        }
    }
}