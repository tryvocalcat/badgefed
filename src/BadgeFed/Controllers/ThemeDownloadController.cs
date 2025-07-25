using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IO;

namespace BadgeFed.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "admin")]
    public class ThemeDownloadController : ControllerBase
    {
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<ThemeDownloadController> _logger;

        public ThemeDownloadController(IWebHostEnvironment environment, ILogger<ThemeDownloadController> logger)
        {
            _environment = environment;
            _logger = logger;
        }

        [HttpGet("{themeName}")]
        public IActionResult DownloadTheme(string themeName)
        {
            try
            {
                // Validate theme name
                if (string.IsNullOrWhiteSpace(themeName) || 
                    !IsValidThemeName(themeName))
                {
                    return BadRequest("Invalid theme name");
                }

                var fileName = $"{themeName}.css";
                var themesPath = Path.Combine(_environment.WebRootPath, "css", "themes");
                var filePath = Path.Combine(themesPath, fileName);

                if (!System.IO.File.Exists(filePath))
                {
                    return NotFound($"Theme '{themeName}' not found");
                }

                var fileBytes = System.IO.File.ReadAllBytes(filePath);
                var contentType = "text/css";

                return File(fileBytes, contentType, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading theme {ThemeName}", themeName);
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
