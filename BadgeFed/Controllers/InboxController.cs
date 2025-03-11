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
        private readonly ActorHelper _actorHelper;
        private readonly ServerConfig _serverConfig;

        public InboxController(
            ILogger<InboxController> logger,
            FollowService followService,
            RepliesService repliesService,
            ActorHelper actorHelper,
            ServerConfig serverConfig)
        {
            _logger = logger;
            _followService = followService;
            _repliesService = repliesService;
            _actorHelper = actorHelper;
            _serverConfig = serverConfig;
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
                _actorHelper.Logger = _logger;

                if (message.IsFollow())
                {
                    await _followService.Follow(message);
                }
                else if (message.IsUndoFollow())
                {
                    await _followService.Unfollow(message);
                    _logger.LogDebug($"Fetching actor information from {message.Actor}");

                    var actor = await ActorHelper.FetchActorInformationAsync(message.Actor);
                    _logger.LogInformation($"Actor: {actor.Id} - {actor.Name} - {actor.Url}");

                    var uuid = Guid.NewGuid();
                    var acceptRequest = new AcceptRequest
                    {
                        Context = "https://www.w3.org/ns/activitystreams",
                        Id = $"{_serverConfig.BaseDomain}/{uuid}",
                        Actor = $"{_serverConfig.BaseDomain}/{_serverConfig.ActorName}",
                        Object = JsonSerializer.Deserialize<dynamic>(JsonSerializer.Serialize(message, options), options)!
                    };

                    var document = JsonSerializer.Serialize(acceptRequest, options);
                    _logger.LogInformation($"Sending accept request to {actor.Inbox} - {document}");
                    await _actorHelper.SendSignedRequest(document, new Uri(actor.Inbox));
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