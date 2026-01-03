using BadgeFed.Models;

namespace BadgeFed.Services
{
    public class BadgeImageService
    {
        private readonly LocalScopedDb _localDbService;
        private readonly OpenBadgeService _openBadgeService;

        public BadgeImageService(LocalScopedDb localDbService, OpenBadgeService openBadgeService)
        {
            _localDbService = localDbService;
            _openBadgeService = openBadgeService;
        }

        public string GetBadgeImageWithMetadataUrl(string baseUrl, string noteId)
        {
            return $"{baseUrl.TrimEnd('/')}/badge-image/{noteId}";
        }

        public async Task<string> GenerateBadgeImageWithMetadata(BadgeRecord badgeRecord, string outputDirectory = null)
        {
            // Get the badge definition
            var badge = _localDbService.GetBadgeById(badgeRecord.Badge.Id);
            if (badge == null)
                throw new InvalidOperationException("Badge definition not found");

            // Generate OpenBadge JSON
            var openBadgeJson = _openBadgeService.GetOpenBadgeJson(badgeRecord);

            // Get the original image path
            var originalImagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", badge.Image.TrimStart('/'));
            if (!File.Exists(originalImagePath))
                throw new FileNotFoundException("Original badge image not found");

            // Determine output path
            if (outputDirectory == null)
            {
                outputDirectory = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "generated-badges");
            }
            
            Directory.CreateDirectory(outputDirectory);
            
            var noteId = badgeRecord.NoteId.Split('/').LastOrDefault();
            var outputPath = Path.Combine(outputDirectory, $"badge-{noteId}-with-metadata.png");

            // Embed the metadata
            ImageService.EmbedOpenBadgeMetadata(originalImagePath, openBadgeJson, outputPath);

            return outputPath;
        }

        public bool ValidateBadgeImage(string imagePath)
        {
            try
            {
                if (!File.Exists(imagePath))
                    return false;

                var hasMetadata = ImageService.HasOpenBadgeMetadata(imagePath);
                if (!hasMetadata)
                    return false;

                var metadata = ImageService.ExtractOpenBadgeMetadata(imagePath);
                
                // Basic JSON validation
                if (string.IsNullOrEmpty(metadata))
                    return false;

                // Try to parse as JSON to validate format
                System.Text.Json.JsonDocument.Parse(metadata);
                
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<string> CreateShareableImageWithMetadata(BadgeRecord badgeRecord, uint width = 672, uint height = 352)
        {
            // First create the regular shareable image
            var badge = _localDbService.GetBadgeById(badgeRecord.Badge.Id);
            var originalImagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", badge.Image.TrimStart('/'));
            
            var tempDir = Path.Combine(Path.GetTempPath(), "badgefed-share-images");
            Directory.CreateDirectory(tempDir);
            
            var noteId = badgeRecord.NoteId.Split('/').LastOrDefault();
            var shareImagePath = Path.Combine(tempDir, $"badge-{noteId}-share.png");
            
            // Create the share-sized image
            ImageService.ModifyImageForPageShare(originalImagePath, width, height);
            
            // Now embed metadata into the share image
            var openBadgeJson = _openBadgeService.GetOpenBadgeJson(badgeRecord);
            var shareImageWithMetadata = Path.Combine(tempDir, $"badge-{noteId}-share-with-metadata.png");
            
            // Get the created share image path
            var shareBasePath = Path.Combine(
                Path.GetDirectoryName(originalImagePath),
                Path.GetFileNameWithoutExtension(originalImagePath) + "-share.png"
            );
            
            ImageService.EmbedOpenBadgeMetadata(shareBasePath, openBadgeJson, shareImageWithMetadata);
            
            return shareImageWithMetadata;
        }
    }
}