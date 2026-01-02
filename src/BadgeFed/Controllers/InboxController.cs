using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using ActivityPubDotNet.Core;
using BadgeFed.Services;
using BadgeFed.Core;

namespace BadgeFed.Controllers
{
    [ApiController]
    [Route("inbox")]
    public class InboxController : ControllerBase
    {
        private readonly ILogger<InboxController> _logger;
        private readonly FollowService _followService;
        private readonly CreateNoteService _createNoteService;
        private readonly BadgeProcessor _badgeProcessor;
        private readonly QuoteRequestService _quoteRequestService;

        private readonly LocalScopedDb _db;
       
        public InboxController(
            ILogger<InboxController> logger,
            FollowService followService,
            CreateNoteService createNoteService,
            BadgeProcessor badgeProcessor,
            QuoteRequestService quoteRequestService,
            LocalScopedDb db)
        {
            _logger = logger;
            _followService = followService;
            _createNoteService = createNoteService;
            _badgeProcessor = badgeProcessor;
            _quoteRequestService = quoteRequestService;
            _db = db;
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
                _quoteRequestService.Logger = _logger;

                if (message.IsFollow())
                {
                    _logger?.LogInformation($"Follow action for actor: {message.Actor}");
                    await _followService.AcceptFollowRequest(message, _db);
                }
                else if (message.IsUndoFollow())
                {
                    _logger?.LogInformation($"Unfollow action for actor: {message.Actor}");
                    await _followService.Unfollow(message);
                }
                else if (message.IsQuoteRequest())
                {
                    _logger?.LogInformation($"Quote request for actor: {message.Actor}");
                    await _quoteRequestService.ProcessQuoteRequest(message);
                }
                else if (message.IsCreateActivity())
                {
                    _logger?.LogInformation($"Create action for actor: {message.Actor}");
                    var result = await _createNoteService.ProcessMessage(message, _db);
                    _logger?.LogInformation($"Create action result: {result.Type}");

                    switch (result.Type)
                    {
                        case CreateNoteResultType.ExternalBadgeProcessed:
                            if (result.BadgeRecord != null)
                            {
                                _logger?.LogInformation($"External badge processed, announcing grant for badge {result.BadgeRecord.Id}");
                                await _badgeProcessor.AnnounceGrantByMainActor(result.BadgeRecord);
                            }
                            else
                            {
                                _logger?.LogWarning("ExternalBadgeProcessed result missing BadgeRecord");
                            }
                            break;
                        case CreateNoteResultType.Error:
                            _logger?.LogError($"Error processing create activity: {result.ErrorMessage}");
                            if (result.Exception != null)
                            {
                                _logger?.LogError(result.Exception, "Exception details");
                            }
                            break;
                        case CreateNoteResultType.Reply:
                            _logger?.LogInformation("Processed as reply to existing badge");
                            break;
                        case CreateNoteResultType.NotProcessed:
                            _logger?.LogInformation("Create activity not processed");
                            break;
                        default:
                            _logger?.LogWarning($"Unknown result type: {result.Type}");
                            break;
                    }
                }
                else if (message.IsAnnounce())
                {
                    _logger?.LogInformation($"Announce action for actor: {message.Actor}");
                    var mainActor = _db.GetMainActor();
                    var result = await _createNoteService.ProcessAnnounce(message, mainActor, _db);
                    _logger?.LogInformation($"Announce action result: {result.Type}");

                    switch (result.Type)
                    {
                        case CreateNoteResultType.ExternalBadgeProcessed:
                            if (result.BadgeRecord != null)
                            {
                                _logger?.LogInformation($"External badge processed via announce, announcing grant for badge {result.BadgeRecord.Id}");
                                await _badgeProcessor.AnnounceGrantByMainActor(result.BadgeRecord);
                            }
                            else
                            {
                                _logger?.LogWarning("ExternalBadgeProcessed result missing BadgeRecord");
                            }
                            break;
                        case CreateNoteResultType.Error:
                            _logger?.LogError($"Error processing announce activity: {result.ErrorMessage}");
                            if (result.Exception != null)
                            {
                                _logger?.LogError(result.Exception, "Exception details");
                            }
                            break;
                        case CreateNoteResultType.Reply:
                            _logger?.LogInformation("Processed as reply to existing badge");
                            break;
                        case CreateNoteResultType.NotProcessed:
                            _logger?.LogInformation("Announce activity not processed");
                            break;
                        default:
                            _logger?.LogWarning($"Unknown result type: {result.Type}");
                            break;
                    }
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