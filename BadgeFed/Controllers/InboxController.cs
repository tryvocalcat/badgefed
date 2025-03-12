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
                    _logger.LogDebug($"Message received: {JsonSerializer.Serialize(message)}");

                    var follower = await ActorHelper.FetchActorInformationAsync(message.Actor);
                    _logger.LogInformation($"Actor: {follower.Id} - {follower.Name} - {follower.Url}");

                    var uuid = Guid.NewGuid();
                   /* var acceptRequest = new AcceptRequest
                    {
                        Context = "https://www.w3.org/ns/activitystreams",
                        Id = $"{_serverConfig.BaseDomain}/{uuid}",
                        Actor = $"{_serverConfig.BaseDomain}/{_serverConfig.ActorName}",
                        Object = JsonSerializer.Deserialize<dynamic>(JsonSerializer.Serialize(message, options), options)!
                    };

                    var document = JsonSerializer.Serialize(acceptRequest, options);*/
                    //_logger.LogInformation($"Sending accept request to {follower.Inbox} - {document}");

                    //var actor = _localDbService.GetActorByFilter($"Uri = \"{target}\"")!;
                    //var actorHelper = new ActorHelper(actor.PrivateKeyPem!, actor.KeyId, Logger);

                    //await actorHelper.SendSignedRequest(document, new Uri(actor.Inbox));
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