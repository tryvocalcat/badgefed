using Microsoft.AspNetCore.Hosting;

namespace BadgeFed.Services;

public class CustomAssetPathService : ICustomAssetPathService
{
    private readonly IWebHostEnvironment _webHostEnvironment;
    private const string CUSTOM_ASSETS_FOLDER = "custom-assets";
    private const string CUSTOM_ASSETS_URL = "/custom-assets";

    public CustomAssetPathService(IWebHostEnvironment webHostEnvironment)
    {
        _webHostEnvironment = webHostEnvironment;
        
        // Ensure all custom asset directories exist
        EnsureDirectoriesExist();
    }

    private void EnsureDirectoriesExist()
    {
        try
        {
            // Create the main custom assets directory
            Directory.CreateDirectory(GetCustomAssetsPath());
            
            // Create all subdirectories
            Directory.CreateDirectory(GetBadgesPath());
            Directory.CreateDirectory(GetAvatarsPath());
            Directory.CreateDirectory(GetPagesPath());
            Directory.CreateDirectory(GetImagesPath());
            Directory.CreateDirectory(GetThemesPath());
            Directory.CreateDirectory(GetCssPath());
        }
        catch (Exception ex)
        {
            // Log the error but don't fail service creation
            // The directories will be created when needed by individual methods
            System.Diagnostics.Debug.WriteLine($"Warning: Could not create custom asset directories: {ex.Message}");
        }
    }

    public string GetCustomAssetsPath()
    {
        return Path.Combine(_webHostEnvironment.WebRootPath, CUSTOM_ASSETS_FOLDER);
    }

    public string GetBadgesPath()
    {
        return Path.Combine(GetCustomAssetsPath(), "badges");
    }

    public string GetAvatarsPath()
    {
        return Path.Combine(GetCustomAssetsPath(), "avatars");
    }

    public string GetPagesPath()
    {
        return Path.Combine(GetCustomAssetsPath(), "pages");
    }

    public string GetImagesPath()
    {
        return Path.Combine(GetCustomAssetsPath(), "img");
    }

    public string GetThemesPath()
    {
        return Path.Combine(GetCustomAssetsPath(), "css", "themes");
    }

    public string GetCssPath()
    {
        return Path.Combine(GetCustomAssetsPath(), "css");
    }

    public string GetCustomAssetPath(params string[] relativePath)
    {
        var allPaths = new[] { GetCustomAssetsPath() }.Concat(relativePath).ToArray();
        return Path.Combine(allPaths);
    }

    public string GetBadgeUrl(string fileName)
    {
        return $"{CUSTOM_ASSETS_URL}/badges/{fileName}";
    }

    public string GetAvatarUrl(string fileName)
    {
        return $"{CUSTOM_ASSETS_URL}/avatars/{fileName}";
    }

    public string GetImageUrl(string fileName)
    {
        return $"{CUSTOM_ASSETS_URL}/img/{fileName}";
    }

    public string GetPageUrl(string fileName)
    {
        return $"{CUSTOM_ASSETS_URL}/pages/{fileName}";
    }

    public string GetThemeUrl(string themeName)
    {
        return $"{CUSTOM_ASSETS_URL}/css/themes/{themeName}.css";
    }

    public string GetCustomCssUrl()
    {
        return $"{CUSTOM_ASSETS_URL}/css/custom.css";
    }
}