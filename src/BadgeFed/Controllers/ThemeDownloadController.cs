using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using BadgeFed.Services;


namespace BadgeFed.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "admin")]
    public class ThemeDownloadController : ControllerBase
    {
        private readonly ICustomAssetPathService _customAssetPathService;
        private readonly ILogger<ThemeDownloadController> _logger;

        public ThemeDownloadController(ICustomAssetPathService customAssetPathService, ILogger<ThemeDownloadController> logger)
        {
            _customAssetPathService = customAssetPathService;
            _logger = logger;
        }

        [HttpGet("{themeName}")]
        public IActionResult DownloadTheme(string themeName)
        {
            _logger.LogInformation("[{RequestHost}] Requesting theme download: {ThemeName}", Request.Host, themeName);
            
            try
            {
                // Validate theme name
                if (string.IsNullOrWhiteSpace(themeName) || 
                    !IsValidThemeName(themeName))
                {
                    _logger.LogWarning("[{RequestHost}] Invalid theme name requested: {ThemeName}", Request.Host, themeName);
                    return BadRequest("Invalid theme name");
                }

                var fileName = $"{themeName}.css";
                var themesPath = _customAssetPathService.GetThemesPath();
                var filePath = Path.Combine(themesPath, fileName);

                if (!System.IO.File.Exists(filePath))
                {
                    _logger.LogWarning("[{RequestHost}] Theme file not found: {ThemeName} at path {FilePath}", Request.Host, themeName, filePath);
                    return NotFound($"Theme '{themeName}' not found");
                }

                _logger.LogInformation("[{RequestHost}] Successfully serving theme: {ThemeName}", Request.Host, themeName);
                var fileBytes = System.IO.File.ReadAllBytes(filePath);
                var contentType = "text/css";

                return File(fileBytes, contentType, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{RequestHost}] Error downloading theme {ThemeName}", Request.Host, themeName);
                return StatusCode(500, "Error downloading theme");
            }
        }

        private static bool IsValidThemeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;
                
            return name.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_') && 
                   name.Length >= 2 && name.Length <= 50;
        }
    }
}
