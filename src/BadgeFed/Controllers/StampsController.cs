using BadgeFed.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BadgeFed.Controllers
{
    /// <summary>
    /// Controller for serving QuoteAuthorization stamps as per FEP-044f.
    /// Uses the "clever alternative" approach where interactingObject and interactionTarget 
    /// are encoded as base64 in the URL, allowing dynamic generation without static file storage.
    /// </summary>
    [ApiController]
    [Route("quote-stamps")]
    public class StampsController : ControllerBase
    {
        private readonly ILogger<StampsController> _logger;
        private readonly LocalScopedDb _db;

        public StampsController(ILogger<StampsController> logger, LocalScopedDb db)
        {
            _logger = logger;
            _db = db;
        }

        /// <summary>
        /// Returns a QuoteAuthorization JSON for the given interactingObject and interactionTarget.
        /// The values are URL-safe base64 encoded in the path.
        /// </summary>
        /// <param name="encodedInteractingObject">Base64 URL-safe encoded URL of the note doing the quoting</param>
        /// <param name="encodedInteractionTarget">Base64 URL-safe encoded URL of the note being quoted (our post)</param>
        [HttpGet("{encodedInteractingObject}/{encodedInteractionTarget}")]
        public IActionResult GetQuoteAuthorization(string encodedInteractingObject, string encodedInteractionTarget)
        {
            try
            {
                // Decode URL-safe base64 values
                var interactingObject = DecodeUrlSafeBase64(encodedInteractingObject);
                var interactionTarget = DecodeUrlSafeBase64(encodedInteractionTarget);

                if (string.IsNullOrEmpty(interactingObject) || string.IsNullOrEmpty(interactionTarget))
                {
                    _logger.LogWarning("Invalid base64 encoded values in stamp request");
                    return BadRequest("Invalid stamp parameters");
                }

                _logger.LogInformation("Generating QuoteAuthorization for interactingObject: {InteractingObject}, interactionTarget: {InteractionTarget}", 
                    interactingObject, interactionTarget);

                // Find the actor that owns the quoted object (interactionTarget)
                var actor = GetActorForQuotedObject(interactionTarget);
                if (actor == null)
                {
                    _logger.LogWarning("Could not find actor for quoted object: {InteractionTarget}", interactionTarget);
                    return NotFound("Quoted object not found");
                }

                var domain = HttpContext.Request.Host.Host;
                var stampId = $"https://{domain}/quote-stamps/{encodedInteractingObject}/{encodedInteractionTarget}";

                // Generate the QuoteAuthorization JSON per FEP-044f
                var quoteAuthorization = new QuoteAuthorization
                {
                    Context = new object[]
                    {
                        "https://www.w3.org/ns/activitystreams",
                        new Dictionary<string, object>
                        {
                            { "QuoteAuthorization", "https://w3id.org/fep/044f#QuoteAuthorization" },
                            { "gts", "https://gotosocial.org/ns#" },
                            { "interactingObject", new Dictionary<string, string> { { "@id", "gts:interactingObject" }, { "@type", "@id" } } },
                            { "interactionTarget", new Dictionary<string, string> { { "@id", "gts:interactionTarget" }, { "@type", "@id" } } }
                        }
                    },
                    Type = "QuoteAuthorization",
                    Id = stampId,
                    AttributedTo = actor.Uri?.ToString() ?? $"https://{domain}/actors/{domain}/{actor.Username}",
                    InteractingObject = interactingObject,
                    InteractionTarget = interactionTarget
                };

                _logger.LogInformation("Successfully generated QuoteAuthorization stamp: {StampId}", stampId);

                return new JsonResult(quoteAuthorization)
                {
                    ContentType = "application/activity+json"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating QuoteAuthorization stamp");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Decodes a URL-safe base64 string back to the original value.
        /// </summary>
        private static string? DecodeUrlSafeBase64(string encoded)
        {
            try
            {
                // Restore standard base64: replace - with +, _ with /
                var base64 = encoded.Replace('-', '+').Replace('_', '/');
                
                // Add padding if necessary
                switch (base64.Length % 4)
                {
                    case 2: base64 += "=="; break;
                    case 3: base64 += "="; break;
                }

                var bytes = Convert.FromBase64String(base64);
                return Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Finds the actor that owns the quoted object.
        /// </summary>
        private Models.Actor? GetActorForQuotedObject(string noteUrl)
        {
            try
            {
                var badgeRecord = _db.GetGrantByNoteId(noteUrl);

                if (badgeRecord == null)
                {
                    _logger.LogInformation("Cannot find badge record for quoted object: {NoteUrl}, using main actor", noteUrl);
                    return _db.GetMainActor();
                }

                var actorUrl = badgeRecord.IssuedBy;
                var actor = _db.GetActorByUri(actorUrl);

                _logger.LogInformation("Found actor {ActorId} for quoted object: {NoteUrl}", actor?.Id, noteUrl);

                return actor ?? _db.GetMainActor();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting actor for quoted object: {NoteUrl}", noteUrl);
                return _db.GetMainActor();
            }
        }
    }

    /// <summary>
    /// Represents a QuoteAuthorization per FEP-044f.
    /// Third-party instances fetch this to verify that a quote has been approved.
    /// </summary>
    public class QuoteAuthorization
    {
        [JsonPropertyName("@context")]
        public object Context { get; set; } = default!;

        [JsonPropertyName("type")]
        public string Type { get; set; } = "QuoteAuthorization";

        [JsonPropertyName("id")]
        public string Id { get; set; } = default!;

        [JsonPropertyName("attributedTo")]
        public string AttributedTo { get; set; } = default!;

        [JsonPropertyName("interactingObject")]
        public string InteractingObject { get; set; } = default!;

        [JsonPropertyName("interactionTarget")]
        public string InteractionTarget { get; set; } = default!;
    }
}
