using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using ActivityPubDotNet.Core;

namespace BadgeFed.Controllers
{
    [ApiController]
    [Route("inbox")]
    public class InboxController : ControllerBase
    {
        private readonly ILogger<InboxController> _logger;
        private readonly FollowService _followService;
        private readonly RepliesService _repliesService;
       
        public InboxController(
            ILogger<InboxController> logger,
            FollowService followService,
            RepliesService repliesService)
        {
            _logger = logger;
            _followService = followService;
            _repliesService = repliesService;
        }

        [HttpPost]
        public async Task<IActionResult> PostInbox([FromBody] InboxMessage? message)
        {
            if (message == null)
            {
                return BadRequest("Invalid message content");
            }

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };

            if (message.IsDelete())
            {
                return StatusCode((int)HttpStatusCode.NotImplemented, "Delete not supported");
            }

            _logger.LogInformation($"Received Activity: {JsonSerializer.Serialize(message, options)}");

            Response.ContentType = "application/activity+json";

            try
            {
                _followService.Logger = _logger;
                
                if (message.IsFollow())
                {
                    await _followService.Follow(message);
                }
                else if (message.IsUndoFollow())
                {
                    await _followService.Unfollow(message);
                }
                else if (message.IsCreateActivity())
                {
                    await _repliesService.AddReply(message);
                }
                else
                {
                    // Handle other activity types or return a default response
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e.ToString());
                return StatusCode((int)HttpStatusCode.InternalServerError, e.Message);
            }

            return Ok();
        }
    }
}