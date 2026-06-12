using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.IO.Compression;
using BadgeFed.Services;

namespace BadgeFed.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "admin")]
    public class StaticPagesController : ControllerBase
    {
        private readonly ICustomAssetPathService _customAssetPathService;
        private readonly LocalScopedDb _localDbService;
        private readonly ILogger<StaticPagesController> _logger;

        public StaticPagesController(
            ICustomAssetPathService customAssetPathService,
            LocalScopedDb localDbService,
            ILogger<StaticPagesController> logger)
        {
            _customAssetPathService = customAssetPathService;
            _localDbService = localDbService;
            _logger = logger;
        }

        [HttpGet("download/{filename}")]
        public IActionResult DownloadSinglePage(string filename)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filename) || !IsValidFilename(filename))
                {
                    return BadRequest("Invalid filename");
                }

                var pagesPath = _customAssetPathService.GetPagesPath();
                var filePath = Path.Combine(pagesPath, filename);

                // Prevent path traversal
                var fullPath = Path.GetFullPath(filePath);
                if (!fullPath.StartsWith(Path.GetFullPath(pagesPath)))
                {
                    return BadRequest("Invalid filename");
                }

                if (!System.IO.File.Exists(fullPath))
                {
                    return NotFound($"Page '{filename}' not found");
                }

                var fileBytes = System.IO.File.ReadAllBytes(fullPath);
                return File(fileBytes, "text/html", filename);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading static page {Filename}", filename);
                return StatusCode(500, "Error downloading static page");
            }
        }

        [HttpGet("download-all")]
        public IActionResult DownloadAllPages()
        {
            try
            {
                var pagesPath = _customAssetPathService.GetPagesPath();

                if (!Directory.Exists(pagesPath))
                {
                    return NotFound("No static pages directory found");
                }

                var htmlFiles = Directory.GetFiles(pagesPath, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(f =>
                    {
                        var ext = Path.GetExtension(f).ToLower();
                        return ext == ".html" || ext == ".htm";
                    })
                    .ToArray();

                if (htmlFiles.Length == 0)
                {
                    return NotFound("No static pages found");
                }

                using var memoryStream = new MemoryStream();
                using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
                {
                    foreach (var file in htmlFiles)
                    {
                        var entryName = Path.GetFileName(file);
                        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
                        using var entryStream = entry.Open();
                        using var fileStream = System.IO.File.OpenRead(file);
                        fileStream.CopyTo(entryStream);
                    }
                }

                memoryStream.Position = 0;
                return File(memoryStream.ToArray(), "application/zip", "static-pages.zip");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading all static pages as zip");
                return StatusCode(500, "Error creating zip archive");
            }
        }

        [HttpPost("upload-zip")]
        [RequestSizeLimit(50 * 1024 * 1024)] // 50MB limit for zip
        public async Task<IActionResult> UploadZip(IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return BadRequest("No file uploaded");
                }

                var extension = Path.GetExtension(file.FileName).ToLower();
                if (extension != ".zip")
                {
                    return BadRequest("Only .zip files are allowed");
                }

                var pagesPath = _customAssetPathService.GetPagesPath();
                Directory.CreateDirectory(pagesPath);

                // Delete existing static pages from disk
                var existingFiles = Directory.GetFiles(pagesPath, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(f =>
                    {
                        var ext = Path.GetExtension(f).ToLower();
                        return ext == ".html" || ext == ".htm";
                    })
                    .ToArray();

                foreach (var existing in existingFiles)
                {
                    System.IO.File.Delete(existing);
                }

                // Extract zip contents
                var extractedPages = new List<string>();
                using (var stream = file.OpenReadStream())
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
                {
                    foreach (var entry in archive.Entries)
                    {
                        var entryExt = Path.GetExtension(entry.FullName).ToLower();
                        if (entryExt != ".html" && entryExt != ".htm")
                        {
                            continue; // Skip non-HTML files
                        }

                        // Use only the filename, ignore directory structure in zip
                        var entryFileName = Path.GetFileName(entry.FullName);
                        if (string.IsNullOrWhiteSpace(entryFileName))
                        {
                            continue; // Skip directory entries
                        }

                        if (!IsValidFilename(entryFileName))
                        {
                            continue; // Skip invalid filenames
                        }

                        var destPath = Path.Combine(pagesPath, entryFileName);

                        // Prevent path traversal
                        var fullDestPath = Path.GetFullPath(destPath);
                        if (!fullDestPath.StartsWith(Path.GetFullPath(pagesPath)))
                        {
                            continue;
                        }

                        using var entryStream = entry.Open();
                        using var fileStream = System.IO.File.Create(fullDestPath);
                        await entryStream.CopyToAsync(fileStream);

                        extractedPages.Add(entryFileName);
                    }
                }

                if (extractedPages.Count == 0)
                {
                    return BadRequest("No HTML files found in the zip archive");
                }

                // Clear existing static pages from database and re-add
                var existingDbPages = _localDbService.GetAllStaticPages();
                foreach (var page in existingDbPages)
                {
                    _localDbService.DeleteStaticPage(page.Id);
                }

                foreach (var pageName in extractedPages)
                {
                    var fileInfo = new FileInfo(Path.Combine(pagesPath, pageName));
                    var staticPage = new BadgeFed.Models.StaticPage
                    {
                        Filename = pageName,
                        Title = Path.GetFileNameWithoutExtension(pageName),
                        Description = "Imported from zip upload",
                        FileSize = fileInfo.Length,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };
                    _localDbService.UpsertStaticPage(staticPage);
                }

                _logger.LogInformation("Uploaded zip with {Count} static pages", extractedPages.Count);

                return Ok(new { message = $"Successfully imported {extractedPages.Count} static page(s)", pages = extractedPages });
            }
            catch (InvalidDataException)
            {
                return BadRequest("The uploaded file is not a valid zip archive");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading static pages zip");
                return StatusCode(500, "Error processing zip upload");
            }
        }

        private static bool IsValidFilename(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            var invalidChars = Path.GetInvalidFileNameChars();
            if (name.Any(c => invalidChars.Contains(c)))
                return false;

            // Prevent path traversal
            if (name.Contains("..") || name.Contains('/') || name.Contains('\\'))
                return false;

            var ext = Path.GetExtension(name).ToLower();
            return ext == ".html" || ext == ".htm";
        }
    }
}
