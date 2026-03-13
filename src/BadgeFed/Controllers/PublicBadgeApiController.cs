using BadgeFed.Services;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace BadgeFed.Controllers
{
    [ApiController]
    [Route("api/badges")]
    public class PublicBadgeApiController : ControllerBase
    {
        private readonly BadgeGrantService _badgeGrantService;
        private readonly LocalScopedDb _localDbService;
        private readonly ILogger<PublicBadgeApiController> _logger;

        public PublicBadgeApiController(BadgeGrantService badgeGrantService, LocalScopedDb localDbService, ILogger<PublicBadgeApiController> logger)
        {
            _badgeGrantService = badgeGrantService;
            _localDbService = localDbService;
            _logger = logger;
        }

        /// <summary>
        /// Grant a badge to a recipient
        /// </summary>
        /// <param name="request">Badge grant request</param>
        /// <returns>Badge grant result with accept URL</returns>
        [HttpPost("grant")]
        [ProducesResponseType(typeof(object), 200)]
        [ProducesResponseType(typeof(object), 400)]
        [ProducesResponseType(typeof(object), 401)]
        [ProducesResponseType(typeof(object), 404)]
        [ProducesResponseType(typeof(object), 409)]
        public IActionResult GrantBadge([FromBody] BadgeGrantRequest request)
        {
            // Check API key authentication
            var apiKey = GetApiKeyFromRequest();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return Unauthorized(new { error = "API key is required. Provide it via X-ApiKey header or apiKey query parameter." });
            }

            var user = _localDbService.ValidateApiKey(apiKey);
            if (user == null)
            {
                _logger.LogWarning("Invalid API key attempt: {ApiKey}", apiKey);
                return Unauthorized(new { error = "Invalid API key." });
            }

            _logger.LogInformation("API request authenticated for user: {UserId} ({UserEmail})", user.Id, user.Email);

            if (request == null)
            {
                return BadRequest(new { error = "Request body is required" });
            }

            // Validate required fields
            if (request.BadgeId <= 0)
            {
                return BadRequest(new { error = "BadgeId is required and must be greater than 0" });
            }

            if (string.IsNullOrWhiteSpace(request.ProfileUri) && string.IsNullOrWhiteSpace(request.Email))
            {
                return BadRequest(new { error = "Either ProfileUri or Email must be provided" });
            }

            // Validate email format if provided
            if (!string.IsNullOrWhiteSpace(request.Email) && !IsValidEmail(request.Email))
            {
                return BadRequest(new { error = "Invalid email format" });
            }

            // Validate URL format if provided
            if (!string.IsNullOrWhiteSpace(request.ProfileUri) && !IsValidUrl(request.ProfileUri))
            {
                return BadRequest(new { error = "Invalid ProfileUri format" });
            }

            var result = _badgeGrantService.GrantBadge(new BadgeGrantService.BadgeGrantRequest
            {
                BadgeId = request.BadgeId,
                ProfileUri = request.ProfileUri?.Trim(),
                Name = request.Name?.Trim(),
                Email = request.Email?.Trim(),
                Evidence = request.Evidence?.Trim()
            });

            if (!result.Success)
            {
                if (result.ErrorMessage?.Contains("not found") == true)
                {
                    return NotFound(new { error = result.ErrorMessage });
                }

                if (result.ErrorMessage?.Contains("already been granted") == true)
                {
                    return Conflict(new { error = result.ErrorMessage });
                }
                
                return BadRequest(new { error = result.ErrorMessage });
            }

            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var fullAcceptUrl = $"{baseUrl}{result.AcceptUrl}";

            return Ok(new
            {
                success = true,
                message = result.Message,
                acceptUrl = fullAcceptUrl,
                badgeRecord = new
                {
                    id = result.BadgeRecord?.Id,
                    title = result.BadgeRecord?.Title,
                    issuedBy = result.BadgeRecord?.IssuedBy,
                    issuedOn = result.BadgeRecord?.IssuedOn,
                    issuedToName = result.BadgeRecord?.IssuedToName,
                    issuedToSubjectUri = result.BadgeRecord?.IssuedToSubjectUri,
                    earningCriteria = result.BadgeRecord?.EarningCriteria
                }
            });
        }

        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

        private bool IsValidUrl(string url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out var uri) && 
                   (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        private string? GetApiKeyFromRequest()
        {
            // First check X-ApiKey header
            if (Request.Headers.TryGetValue("X-ApiKey", out var headerValues))
            {
                var headerValue = headerValues.FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(headerValue))
                {
                    return headerValue.Trim();
                }
            }

            // Then check apiKey query parameter
            if (Request.Query.TryGetValue("apiKey", out var queryValues))
            {
                var queryValue = queryValues.FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(queryValue))
                {
                    return queryValue.Trim();
                }
            }

            return null;
        }

        /// <summary>
        /// Get the status of a badge grant by its NoteId
        /// </summary>
        /// <param name="noteId">The NoteId (grant ID) of the badge record</param>
        /// <returns>Grant status with limited information</returns>
        [HttpGet("grant/{noteId}/status")]
        [ProducesResponseType(typeof(object), 200)]
        [ProducesResponseType(typeof(object), 401)]
        [ProducesResponseType(typeof(object), 404)]
        public IActionResult GetGrantStatus(string noteId)
        {
            var apiKey = GetApiKeyFromRequest();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return Unauthorized(new { error = "API key is required. Provide it via X-ApiKey header or apiKey query parameter." });
            }

            var user = _localDbService.ValidateApiKey(apiKey);
            if (user == null)
            {
                _logger.LogWarning("Invalid API key attempt: {ApiKey}", apiKey);
                return Unauthorized(new { error = "Invalid API key." });
            }

            _logger.LogInformation("API request to get grant status for NoteId: {NoteId} by user: {UserId}", noteId, user.Id);

            var record = _localDbService.GetGrantByNoteId(noteId);

            if (record == null)
            {
                return NotFound(new { error = "Grant not found." });
            }

            // Verify the authenticated user owns the badge associated with this grant
            if (record.Badge?.Id > 0)
            {
                var ownershipFilter = $"a.OwnerId = '{user.GroupId}' AND b.Id = {record.Badge.Id}";
                var ownedBadges = _localDbService.GetAllBadgeDefinitions(true, ownershipFilter);
                if (!ownedBadges.Any())
                {
                    return NotFound(new { error = "Grant not found." });
                }
            }

            return Ok(new
            {
                success = true,
                noteId = record.NoteId,
                profileUri = record.IssuedToSubjectUri,
                badgeId = record.Badge?.Id,
                status = record.Status
            });
        }

        /// <summary>
        /// List all active badges owned by the authenticated user
        /// </summary>
        /// <returns>List of badge definitions</returns>
        [HttpGet]
        [ProducesResponseType(typeof(object), 200)]
        [ProducesResponseType(typeof(object), 401)]
        public IActionResult ListBadges()
        {
            var apiKey = GetApiKeyFromRequest();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return Unauthorized(new { error = "API key is required. Provide it via X-ApiKey header or apiKey query parameter." });
            }

            var user = _localDbService.ValidateApiKey(apiKey);
            if (user == null)
            {
                _logger.LogWarning("Invalid API key attempt: {ApiKey}", apiKey);
                return Unauthorized(new { error = "Invalid API key." });
            }

            _logger.LogInformation("API request to list badges for user: {UserId} ({UserEmail})", user.Id, user.Email);

            var filter = $"a.OwnerId = '{user.GroupId}'";
            var badges = _localDbService.GetAllBadgeDefinitions(true, filter);

            return Ok(new
            {
                success = true,
                count = badges.Count,
                badges = badges.Select(b => new
                {
                    id = b.Id,
                    title = b.Title,
                    description = b.Description,
                    badgeType = b.BadgeType,
                    earningCriteria = b.EarningCriteria,
                    image = b.Image,
                    imageAltText = b.ImageAltText,
                    hashtags = b.Hashtags,
                    infoUri = b.InfoUri,
                    isCertificate = b.IsCertificate,
                    issuer = b.Issuer != null ? new
                    {
                        id = b.Issuer.Id,
                        fullName = b.Issuer.FullName,
                        username = b.Issuer.Username,
                        domain = b.Issuer.Domain
                    } : null
                })
            });
        }

        /// <summary>
        /// Get badge information
        /// </summary>
        /// <param name="badgeId">Badge ID</param>
        /// <returns>Badge information</returns>
        [HttpGet("{badgeId}")]
        [ProducesResponseType(typeof(object), 200)]
        [ProducesResponseType(typeof(object), 401)]
        [ProducesResponseType(typeof(object), 404)]
        public IActionResult GetBadge(long badgeId)
        {
            var apiKey = GetApiKeyFromRequest();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return Unauthorized(new { error = "API key is required. Provide it via X-ApiKey header or apiKey query parameter." });
            }

            var user = _localDbService.ValidateApiKey(apiKey);
            if (user == null)
            {
                _logger.LogWarning("Invalid API key attempt: {ApiKey}", apiKey);
                return Unauthorized(new { error = "Invalid API key." });
            }

            _logger.LogInformation("[{RequestHost}] API request to get badge: {BadgeId}", Request.Host, badgeId);

            var filter = $"a.OwnerId = '{user.GroupId}' AND b.Id = {badgeId}";
            var badges = _localDbService.GetAllBadgeDefinitions(true, filter);
            var badge = badges.FirstOrDefault();

            if (badge == null)
            {
                return NotFound(new { error = "Badge not found or not authorized." });
            }

            return Ok(new
            {
                success = true,
                badge = new
                {
                    id = badge.Id,
                    title = badge.Title,
                    description = badge.Description,
                    badgeType = badge.BadgeType,
                    earningCriteria = badge.EarningCriteria,
                    image = badge.Image,
                    imageAltText = badge.ImageAltText,
                    hashtags = badge.Hashtags,
                    infoUri = badge.InfoUri,
                    isCertificate = badge.IsCertificate,
                    issuer = badge.Issuer != null ? new
                    {
                        id = badge.Issuer.Id,
                        fullName = badge.Issuer.FullName,
                        username = badge.Issuer.Username,
                        domain = badge.Issuer.Domain
                    } : null
                }
            });
        }
    }

    public class BadgeGrantRequest
    {
        /// <summary>
        /// The ID of the badge to grant
        /// </summary>
        [Required]
        [Range(1, long.MaxValue, ErrorMessage = "BadgeId must be greater than 0")]
        public long BadgeId { get; set; }

        /// <summary>
        /// Profile URI of the recipient (optional, but either this or Email must be provided)
        /// </summary>
        [Url(ErrorMessage = "ProfileUri must be a valid URL")]
        [StringLength(500, ErrorMessage = "ProfileUri cannot exceed 500 characters")]
        public string? ProfileUri { get; set; }

        /// <summary>
        /// Name of the recipient (optional)
        /// </summary>
        [StringLength(200, ErrorMessage = "Name cannot exceed 200 characters")]
        public string? Name { get; set; }

        /// <summary>
        /// Email of the recipient (optional, but either this or ProfileUri must be provided)
        /// </summary>
        [EmailAddress(ErrorMessage = "Invalid email format")]
        [StringLength(254, ErrorMessage = "Email cannot exceed 254 characters")]
        public string? Email { get; set; }

        /// <summary>
        /// Evidence or reason for granting the badge (optional)
        /// </summary>
        [StringLength(2000, ErrorMessage = "Evidence cannot exceed 2000 characters")]
        public string? Evidence { get; set; }
    }
}
