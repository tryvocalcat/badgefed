namespace BadgeFed.Services;

public interface ICustomAssetPathService
{
    /// <summary>
    /// Gets the base path for custom assets within the web root
    /// </summary>
    string GetCustomAssetsPath();
    
    /// <summary>
    /// Gets the path for badge uploads
    /// </summary>
    string GetBadgesPath();
    
    /// <summary>
    /// Gets the path for avatar uploads
    /// </summary>
    string GetAvatarsPath();
    
    /// <summary>
    /// Gets the path for custom pages
    /// </summary>
    string GetPagesPath();
    
    /// <summary>
    /// Gets the path for custom images
    /// </summary>
    string GetImagesPath();
    
    /// <summary>
    /// Gets the path for custom themes
    /// </summary>
    string GetThemesPath();
    
    /// <summary>
    /// Gets the path for custom CSS files
    /// </summary>
    string GetCssPath();
    
    /// <summary>
    /// Gets a file path within the custom assets directory
    /// </summary>
    /// <param name="relativePath">Relative path components</param>
    string GetCustomAssetPath(params string[] relativePath);

    string GetCustomAssetUrl(string asset);
    
    /// <summary>
    /// Gets the URL for an uploaded badge image
    /// </summary>
    /// <param name="fileName">The badge image filename</param>
    string GetBadgeUrl(string fileName);
    
    /// <summary>
    /// Gets the URL for an uploaded avatar image
    /// </summary>
    /// <param name="fileName">The avatar image filename</param>
    string GetAvatarUrl(string fileName);
    
    /// <summary>
    /// Gets the URL for a custom image
    /// </summary>
    /// <param name="fileName">The image filename</param>
    string GetImageUrl(string fileName);
    
    /// <summary>
    /// Gets the URL for a custom page
    /// </summary>
    /// <param name="fileName">The page filename</param>
    string GetPageUrl(string fileName);
    
    /// <summary>
    /// Gets the URL for a custom theme
    /// </summary>
    /// <param name="themeName">The theme name</param>
    string GetThemeUrl(string themeName);
    
    /// <summary>
    /// Gets the URL for the custom CSS file
    /// </summary>
    string GetCustomCssUrl();
}