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
        private readonly CreateNoteService _createNoteService;
       
        public InboxController(
            ILogger<InboxController> logger,
            FollowService followService,
            CreateNoteService createNoteService)
        {
            _logger = logger;
            _followService = followService;
            _createNoteService = createNoteService;
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
                _createNoteService.Logger = _logger;

                if (message.IsFollow())
                {
                    _logger?.LogInformation($"Follow action for actor: {message.Actor}");
                    await _followService.AcceptFollowRequest(message);
                }
                else if (message.IsUndoFollow())
                {
                    _logger?.LogInformation($"Unfollow action for actor: {message.Actor}");
                    await _followService.Unfollow(message);
                }
                else if (message.IsCreateActivity())
                {
                    _logger?.LogInformation($"Create action for actor: {message.Actor}");
                    await _createNoteService.ProcessMessage(message);
                }
                else if (message.IsAnnounce())
                {
                    _logger?.LogInformation($"Announce action for actor: {message.Actor}");
                    await _createNoteService.ProcessAnnounce(message);
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