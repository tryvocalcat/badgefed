using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BadgeFed.Services;

namespace BadgeFed.Controllers
{
    [ApiController]
    [Route("api/mastodon")]
    [Authorize]
    public class MastodonInteractionController : ControllerBase
    {
        private readonly MastodonClientService _mastodonClient;
        private readonly ILogger<MastodonInteractionController> _logger;

        public MastodonInteractionController(MastodonClientService mastodonClient, ILogger<MastodonInteractionController> logger)
        {
            _mastodonClient = mastodonClient;
            _logger = logger;
        }

        /// <summary>
        /// Resolve a remote status URL on the user's instance.
        /// </summary>
        [HttpPost("resolve")]
        public async Task<IActionResult> Resolve([FromBody] ResolveRequest request)
        {
            var (host, token) = await GetUserContext();
            if (host == null || token == null)
                return Unauthorized(new { error = "Not authenticated with a Mastodon-compatible instance" });

            var result = await _mastodonClient.ResolveStatusAsync(host, token, request.StatusUrl);
            if (result == null)
                return NotFound(new { error = "Could not resolve status on your instance" });

            return Ok(result);
        }

        /// <summary>
        /// Like (favourite) a status.
        /// </summary>
        [HttpPost("favourite")]
        public async Task<IActionResult> Favourite([FromBody] StatusActionRequest request)
        {
            var (host, token) = await GetUserContext();
            if (host == null || token == null)
                return Unauthorized(new { error = "Not authenticated with a Mastodon-compatible instance" });

            var result = await _mastodonClient.FavouriteAsync(host, token, request.StatusId);
            if (result == null)
                return BadRequest(new { error = "Failed to favourite status" });

            return Ok(result);
        }

        /// <summary>
        /// Unlike (unfavourite) a status.
        /// </summary>
        [HttpPost("unfavourite")]
        public async Task<IActionResult> Unfavourite([FromBody] StatusActionRequest request)
        {
            var (host, token) = await GetUserContext();
            if (host == null || token == null)
                return Unauthorized(new { error = "Not authenticated with a Mastodon-compatible instance" });

            var result = await _mastodonClient.UnfavouriteAsync(host, token, request.StatusId);
            if (result == null)
                return BadRequest(new { error = "Failed to unfavourite status" });

            return Ok(result);
        }

        /// <summary>
        /// Boost (reblog) a status.
        /// </summary>
        [HttpPost("boost")]
        public async Task<IActionResult> Boost([FromBody] StatusActionRequest request)
        {
            var (host, token) = await GetUserContext();
            if (host == null || token == null)
                return Unauthorized(new { error = "Not authenticated with a Mastodon-compatible instance" });

            var result = await _mastodonClient.BoostAsync(host, token, request.StatusId);
            if (result == null)
                return BadRequest(new { error = "Failed to boost status" });

            return Ok(result);
        }

        /// <summary>
        /// Unboost (unreblog) a status.
        /// </summary>
        [HttpPost("unboost")]
        public async Task<IActionResult> Unboost([FromBody] StatusActionRequest request)
        {
            var (host, token) = await GetUserContext();
            if (host == null || token == null)
                return Unauthorized(new { error = "Not authenticated with a Mastodon-compatible instance" });

            var result = await _mastodonClient.UnboostAsync(host, token, request.StatusId);
            if (result == null)
                return BadRequest(new { error = "Failed to unboost status" });

            return Ok(result);
        }

        /// <summary>
        /// Reply (comment) to a status.
        /// </summary>
        [HttpPost("reply")]
        public async Task<IActionResult> Reply([FromBody] ReplyRequest request)
        {
            var (host, token) = await GetUserContext();
            if (host == null || token == null)
                return Unauthorized(new { error = "Not authenticated with a Mastodon-compatible instance" });

            if (string.IsNullOrWhiteSpace(request.Content))
                return BadRequest(new { error = "Reply content cannot be empty" });

            var result = await _mastodonClient.ReplyAsync(host, token, request.StatusId, request.Content, request.MediaIds);
            if (result == null)
                return BadRequest(new { error = "Failed to post reply" });

            return Ok(result);
        }

        /// <summary>
        /// Upload a media file for attaching to a reply.
        /// </summary>
        [HttpPost("upload")]
        [RequestSizeLimit(10 * 1024 * 1024)] // 10MB limit
        public async Task<IActionResult> Upload(IFormFile file, [FromForm] string? description)
        {
            var (host, token) = await GetUserContext();
            if (host == null || token == null)
                return Unauthorized(new { error = "Not authenticated with a Mastodon-compatible instance" });

            if (file == null || file.Length == 0)
                return BadRequest(new { error = "No file provided" });

            // Validate file type
            var allowedTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/webp" };
            if (!allowedTypes.Contains(file.ContentType.ToLowerInvariant()))
                return BadRequest(new { error = "Only image files (JPEG, PNG, GIF, WebP) are allowed" });

            using var stream = file.OpenReadStream();
            var result = await _mastodonClient.UploadMediaAsync(host, token, stream, file.FileName, file.ContentType, description);
            if (result == null)
                return BadRequest(new { error = "Failed to upload media" });

            return Ok(result);
        }

        /// <summary>
        /// Follow an account (issuer).
        /// </summary>
        [HttpPost("follow")]
        public async Task<IActionResult> Follow([FromBody] FollowRequest request)
        {
            var (host, token) = await GetUserContext();
            if (host == null || token == null)
                return Unauthorized(new { error = "Not authenticated with a Mastodon-compatible instance" });

            var result = await _mastodonClient.FollowAccountAsync(host, token, request.AccountUri);
            if (result == null)
                return BadRequest(new { error = "Failed to follow account" });

            return Ok(result);
        }

        /// <summary>
        /// Unfollow an account.
        /// </summary>
        [HttpPost("unfollow")]
        public async Task<IActionResult> Unfollow([FromBody] FollowRequest request)
        {
            var (host, token) = await GetUserContext();
            if (host == null || token == null)
                return Unauthorized(new { error = "Not authenticated with a Mastodon-compatible instance" });

            var result = await _mastodonClient.UnfollowAccountAsync(host, token, request.AccountUri);
            if (result == null)
                return BadRequest(new { error = "Failed to unfollow account" });

            return Ok(result);
        }

        /// <summary>
        /// Check the user's authentication status and return their instance info.
        /// </summary>
        [HttpGet("status")]
        [AllowAnonymous]
        public async Task<IActionResult> GetStatus()
        {
            var (host, token) = await GetUserContext();
            if (host == null || token == null)
                return Ok(new { authenticated = false });

            var username = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
            return Ok(new { authenticated = true, instance = host, username });
        }

        private async Task<(string? host, string? token)> GetUserContext()
        {
            var host = User.FindFirst("urn:mastodon:hostname")?.Value
                    ?? User.FindFirst("urn:gotosocial:hostname")?.Value;

            if (string.IsNullOrEmpty(host))
                return (null, null);

            var token = await HttpContext.GetTokenAsync("access_token");
            if (string.IsNullOrEmpty(token))
                return (null, null);

            return (host, token);
        }
    }

    public class ResolveRequest
    {
        public string StatusUrl { get; set; } = string.Empty;
    }

    public class StatusActionRequest
    {
        public string StatusId { get; set; } = string.Empty;
    }

    public class ReplyRequest
    {
        public string StatusId { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public List<string>? MediaIds { get; set; }
    }

    public class FollowRequest
    {
        public string AccountUri { get; set; } = string.Empty;
    }
}
