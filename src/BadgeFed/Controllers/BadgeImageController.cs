using Microsoft.AspNetCore.Mvc;
using BadgeFed.Services;
using BadgeFed.Models;

namespace BadgeFed.Controllers
{
    [Route("badge-image")]
    [ApiController]
    public class BadgeImageController : ControllerBase
    {
        private readonly LocalScopedDb _localDbService;
        private readonly OpenBadgeService _openBadgeService;
        private readonly ILogger<BadgeImageController> _logger;

        public BadgeImageController(LocalScopedDb localDbService, OpenBadgeService openBadgeService, ILogger<BadgeImageController> logger)
        {
            _localDbService = localDbService;
            _openBadgeService = openBadgeService;
            _logger = logger;
        }

        [HttpGet("{noteId}")]
        public async Task<IActionResult> GetBadgeImageWithMetadata(string noteId)
        {
            _logger.LogInformation("[{RequestHost}] Generating badge image with metadata for noteId: {NoteId}", Request.Host, noteId);
            
            try
            {
                // Get the badge record
                var badgeRecord = _openBadgeService.GetOpenBadgeFromBadgeRecord(noteId);
                if (badgeRecord == null)
                {
                    _logger.LogWarning("[{RequestHost}] Badge record not found for noteId: {NoteId}", Request.Host, noteId);
                    return NotFound("Badge record not found");
                }

                // Generate OpenBadge JSON
                var openBadgeJson = _openBadgeService.GetOpenBadgeJson(badgeRecord);

                // Get the original image path
                var badge = _localDbService.GetBadgeById(badgeRecord.Badge.Id);
                if (badge == null)
                {
                    _logger.LogWarning("[{RequestHost}] Badge definition not found for badge ID: {BadgeId}", Request.Host, badgeRecord.Badge.Id);
                    return NotFound("Badge definition not found");
                }

                var originalImagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", badge.Image.TrimStart('/'));
                if (!System.IO.File.Exists(originalImagePath))
                {
                    _logger.LogError("[{RequestHost}] Badge image file not found at path: {ImagePath}", Request.Host, originalImagePath);
                    return NotFound("Badge image file not found");
                }
                
                _logger.LogInformation("[{RequestHost}] Successfully generated badge image with metadata for noteId: {NoteId}", Request.Host, noteId);

                // Create a temporary file with embedded metadata
                var tempDir = Path.Combine(Path.GetTempPath(), "badgefed-images");
                Directory.CreateDirectory(tempDir);
                
                var outputPath = Path.Combine(tempDir, $"badge-{noteId}-with-metadata.png");

                // Check if we already have a cached version
                if (System.IO.File.Exists(outputPath))
                {
                    var cachedFileInfo = new FileInfo(outputPath);
                    var originalFileInfo = new FileInfo(originalImagePath);
                    
                    // If cached file is newer than original, use it
                    if (cachedFileInfo.LastWriteTime >= originalFileInfo.LastWriteTime)
                    {
                        var cachedImageBytes = await System.IO.File.ReadAllBytesAsync(outputPath);
                        return File(cachedImageBytes, "image/png", $"badge-{noteId}.png");
                    }
                }

                // Embed the metadata
                ImageService.EmbedOpenBadgeMetadata(originalImagePath, openBadgeJson, outputPath);

                // Return the image with embedded metadata
                var imageBytes = await System.IO.File.ReadAllBytesAsync(outputPath);
                
                // Set headers to indicate this is a verifiable badge
                Response.Headers.Add("X-OpenBadge-Embedded", "true");
                Response.Headers.Add("X-Badge-Verification-Url", $"{Request.Scheme}://{Request.Host}/verify/{noteId}");
                
                return File(imageBytes, "image/png", $"badge-{noteId}.png");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{RequestHost}] Error generating badge image for noteId: {NoteId}", Request.Host, noteId);
                return StatusCode(500, $"Error generating badge image: {ex.Message}");
            }
        }

        [HttpGet("{noteId}/metadata")]
        public IActionResult GetBadgeMetadata(string noteId)
        {
            _logger.LogInformation("[{RequestHost}] Fetching badge metadata for noteId: {NoteId}", Request.Host, noteId);
            
            try
            {
                var badgeRecord = _openBadgeService.GetOpenBadgeFromBadgeRecord(noteId);
                if (badgeRecord == null)
                {
                    _logger.LogWarning("[{RequestHost}] Badge record not found when fetching metadata for noteId: {NoteId}", Request.Host, noteId);
                    return NotFound("Badge record not found");
                }
                
                _logger.LogInformation("[{RequestHost}] Successfully retrieved badge metadata for noteId: {NoteId}", Request.Host, noteId);
                var openBadgeJson = _openBadgeService.GetOpenBadgeJson(badgeRecord);
                return Content(openBadgeJson, "application/json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{RequestHost}] Error generating badge metadata for noteId: {NoteId}", Request.Host, noteId);
                return StatusCode(500, $"Error generating badge metadata: {ex.Message}");
            }
        }

        [HttpPost("extract-metadata")]
        public async Task<IActionResult> ExtractMetadataFromImage(IFormFile imageFile)
        {
            _logger.LogInformation("[{RequestHost}] Extracting metadata from uploaded image file: {FileName}", Request.Host, imageFile?.FileName ?? "unknown");
            
            if (imageFile == null || imageFile.Length == 0)
            {
                _logger.LogWarning("[{RequestHost}] No image file provided for metadata extraction", Request.Host);
                return BadRequest("No image file provided");
            }

            try
            {
                // Save uploaded file temporarily
                var tempPath = Path.GetTempFileName();
                using (var stream = new FileStream(tempPath, FileMode.Create))
                {
                    await imageFile.CopyToAsync(stream);
                }

                // Extract metadata
                var metadata = ImageService.ExtractOpenBadgeMetadata(tempPath);
                
                // Clean up temp file
                System.IO.File.Delete(tempPath);

                if (string.IsNullOrEmpty(metadata))
                {
                    _logger.LogWarning("[{RequestHost}] No OpenBadge metadata found in uploaded image: {FileName}", Request.Host, imageFile.FileName);
                    return NotFound("No OpenBadge metadata found in image");
                }

                _logger.LogInformation("[{RequestHost}] Successfully extracted metadata from image: {FileName}", Request.Host, imageFile.FileName);
                return Content(metadata, "application/json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{RequestHost}] Error extracting metadata from image: {FileName}", Request.Host, imageFile?.FileName ?? "unknown");
                return StatusCode(500, $"Error extracting metadata: {ex.Message}");
            }
        }
    }
}