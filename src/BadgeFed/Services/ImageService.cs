
using ImageMagick;

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
                Path.GetFileNameWithoutExtension(filePath) + "-share" + Path.GetExtension(filePath)
            );

            image.Write(newFilePath);
        }
    }
}