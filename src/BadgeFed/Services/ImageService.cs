
using ImageMagick;
using System.Text;

namespace BadgeFed.Services;

public class ImageService
{
    public static void ModifyImageForPageShare(string filePath, uint width = 672, uint height = 352)
    {
        // the image will be centered, not modified in aspect, put in a transparent canvas of 672x352, and the rest will be filled with transparent
        // we need to detect the aspect of the image, to decide if we set the max width to 672 or height to 352, usually are square 1:1

        using (var image = new MagickImage(filePath))
        {
            var aspect = (double)image.Width / image.Height;

            if (aspect > 1)
            {
                // landscape
                image.Resize(width, 0);
            }
            else
            {
                // portrait
                image.Resize(0, height);
            }

            image.BackgroundColor = MagickColors.Transparent;
            image.Extent(width, height, Gravity.Center, MagickColors.Transparent);

            var newFilePath = Path.Combine(
                Path.GetDirectoryName(filePath),
                Path.GetFileNameWithoutExtension(filePath) + "-share.png"
            );

            image.Format = MagickFormat.Png;
            image.Write(newFilePath);
        }
    }

    public static string EmbedOpenBadgeMetadata(string sourceImagePath, string openBadgeJson, string outputPath = null)
    {
        if (outputPath == null)
        {
            var directory = Path.GetDirectoryName(sourceImagePath);
            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(sourceImagePath);
            outputPath = Path.Combine(directory, $"{fileNameWithoutExt}-with-metadata.png");
        }

        using (var image = new MagickImage(sourceImagePath))
        {
            // Ensure we're working with PNG format
            image.Format = MagickFormat.Png;
            
            // Add OpenBadge metadata to PNG tEXt chunk
            image.SetAttribute("openbadge", openBadgeJson);
            
            // Also add a verification URL as a comment
            image.Comment = $"This is a verified OpenBadge. Metadata embedded: {DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}";
            
            image.Write(outputPath);
        }

        return outputPath;
    }

    public static string ExtractOpenBadgeMetadata(string imagePath)
    {
        using (var image = new MagickImage(imagePath))
        {
            return image.GetAttribute("openbadge");
        }
    }

    public static bool HasOpenBadgeMetadata(string imagePath)
    {
        try
        {
            using (var image = new MagickImage(imagePath))
            {
                var metadata = image.GetAttribute("openbadge");
                return !string.IsNullOrEmpty(metadata);
            }
        }
        catch
        {
            return false;
        }
    }
}