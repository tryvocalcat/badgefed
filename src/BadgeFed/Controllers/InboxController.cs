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
        private readonly JobQueueService _jobQueue;

        private readonly LocalScopedDb _db;

        public InboxController(
            ILogger<InboxController> logger,
            FollowService followService,
            CreateNoteService createNoteService,
            BadgeProcessor badgeProcessor,
            QuoteRequestService quoteRequestService,
            JobQueueService jobQueue,
            LocalScopedDb db)
        {
            _logger = logger;
            _followService = followService;
            _createNoteService = createNoteService;
            _badgeProcessor = badgeProcessor;
            _quoteRequestService = quoteRequestService;
            _jobQueue = jobQueue;
            _db = db;
        }

        [HttpPost]
        public async Task<IActionResult> PostInbox([FromBody] InboxMessage? message)
        {
            if (message == null)
            {
                return BadRequest("Invalid message content");
            }

            if (message.IsDelete())
            {
                return StatusCode((int)HttpStatusCode.NotImplemented, "Delete not supported");
            }

            _logger.LogInformation($"Received Activity: {message.Type} - {message.Id}");

            Response.ContentType = "application/activity+json";

            try
            {
                _followService.Logger = _logger;
                _createNoteService.Logger = _logger;
                _quoteRequestService.Logger = _logger;

                if (message.IsFollow())
                {
                    _logger?.LogInformation($"Follow action for actor: {message.Actor}");
                    
                    // Insert job into queue
                    await _jobQueue.AddJobAsync("accept_follow_request", message, createdBy: "InboxController");
                }
                else if (message.IsUndoFollow())
                {
                    _logger?.LogInformation($"Unfollow action for actor: {message.Actor}");
                    
                    // Insert job into queue
                    await _jobQueue.AddJobAsync("unfollow", message, createdBy: "InboxController");
                }
                else if (message.IsQuoteRequest())
                {
                    _logger?.LogInformation($"Quote request for actor: {message.Actor}");
                    
                    // Insert job into queue
                    await _jobQueue.AddJobAsync("process_quote_request", message, createdBy: "InboxController");
                }
                else if (message.IsCreateActivity())
                {
                    _logger?.LogInformation($"Create action for actor: {message.Actor}");
                    
                    // Insert job into queue
                    await _jobQueue.AddJobAsync("create_activity", message, createdBy: "InboxController");
                }
                else if (message.IsAnnounce())
                {
                    _logger?.LogInformation($"Announce action for actor: {message.Actor}");
                    
                    // Insert job into queue
                    await _jobQueue.AddJobAsync("process_announce", message, createdBy: "InboxController");
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