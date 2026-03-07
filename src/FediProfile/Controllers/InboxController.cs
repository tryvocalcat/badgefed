using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using FediProfile.Models;
using FediProfile.Services;

namespace FediProfile.Controllers;

[ApiController]
[Route("{userSlug}/inbox")]
public class InboxController : ControllerBase
{
    private readonly ILogger<InboxController> _logger;
    private readonly FollowService _followService;
    private readonly AnnounceService _announceService;
    private readonly LocalDbFactory _factory;
    private readonly ActorService _actorService;

    public InboxController(
        ILogger<InboxController> logger,
        FollowService followService,
        AnnounceService announceService,
        LocalDbFactory factory,
        ActorService actorService)
    {
        _logger = logger;
        _followService = followService;
        _announceService = announceService;
        _factory = factory;
        _actorService = actorService;
    }

    [HttpPost]
    public async Task<IActionResult> PostInbox(string userSlug, [FromBody] JsonElement? message)
    {
        Console.WriteLine($"Received inbox message for user '{userSlug}': {message}");

        if (message == null)
        {
            _logger.LogWarning("Received null message");
            return BadRequest("Invalid message content");
        }

        // Get the user-scoped database for this user slug
        var domain = HttpContext.Request.Host.Host;
        var db = _factory.GetInstance(domain, userSlug);
        
        // Determine the actor identity from the request context
        var scheme = HttpContext.Request.Scheme;
        var fullDomain = HttpContext.Request.Host.ToString();
        var actorId = $"{scheme}://{fullDomain}/{userSlug}";

        try
        {
            var inboxMsg = JsonSerializer.Deserialize<InboxMessage>(message.Value.GetRawText());

            if (inboxMsg == null)
            {
                _logger.LogWarning("Could not deserialize message");
                return BadRequest("Invalid message format");
            }

            _logger.LogInformation($"Received Activity: {inboxMsg.Type} from {inboxMsg.Actor} for user {userSlug}");

            if (inboxMsg.IsFollow())
            {
                Console.WriteLine($"Handling Follow activity from {inboxMsg.Actor}");
                await _followService.HandleFollowAsync(inboxMsg, db, actorId);
            }
            else if (inboxMsg.IsUndo() && inboxMsg.GetFollowActor() != null)
            {
                await _followService.HandleUnfollowAsync(inboxMsg, db);
            }
            else if (inboxMsg.IsCreate())
            {
                await _announceService.HandleCreateActivityAsync(inboxMsg, db, actorId);
            }
            else if (inboxMsg.IsAnnounce())
            {
                _logger.LogInformation($"Received Announce from {inboxMsg.Actor}, ignoring");
            }
            else
            {
                _logger.LogInformation($"Ignoring activity type: {inboxMsg.Type}");
            }

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing inbox message");
            return StatusCode(500, "Error processing message");
        }
    }
}
