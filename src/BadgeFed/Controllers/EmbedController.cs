using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using BadgeFed.Services;
using BadgeFed.Models;
using System.Text;
using Microsoft.AspNetCore.Cors;

namespace BadgeFed.Controllers
{
    [ApiController]
    [Route("api/embed")]
    [EnableCors("EmbedPolicy")]
    public class EmbedController : ControllerBase
    {
        private readonly LocalScopedDb _localDbService;
        private readonly OpenBadgeService _openBadgeService;
        private readonly ILogger<EmbedController> _logger;

        public EmbedController(LocalScopedDb localDbService, OpenBadgeService openBadgeService, ILogger<EmbedController> logger)
        {
            _localDbService = localDbService;
            _openBadgeService = openBadgeService;
            _logger = logger;
        }

        [HttpGet("badges")]
        public IActionResult GetBadgesByRecipient([FromQuery] string recipient, [FromQuery] int limit = 10, [FromQuery] string format = "openbadges")
        {
            if (string.IsNullOrWhiteSpace(recipient))
            {
                return BadRequest(new { error = "Recipient parameter is required" });
            }

            try
            {
                _logger.LogInformation("Fetching badges for recipient: {Recipient}", recipient);
                
                // Support both encoded and non-encoded recipient URIs
                string decodedRecipient;
                try
                {
                    var bytes = Convert.FromBase64String(recipient);
                    decodedRecipient = Encoding.UTF8.GetString(bytes);
                }
                catch
                {
                    decodedRecipient = recipient;
                }

                // Validate limit
                limit = Math.Min(Math.Max(limit, 1), 50);

                var filter = $@"
                    FingerPrint IS NOT NULL 
                    AND NoteId IS NOT NULL 
                    AND Visibility = 'Public'
                    AND (IssuedToSubjectUri = '{decodedRecipient}' OR IssuedToSubjectUri = '{recipient}')
                    ORDER BY IssuedOn DESC
                    LIMIT {limit}
                ";

                var badgeRecords = _localDbService.GetBadgeRecords(filter);
                _logger.LogInformation("Found {Count} badges for recipient", badgeRecords?.Count ?? 0);

                if (format.ToLower() == "openbadges")
                {
                    var openBadges = new List<object>();
                    
                    foreach (var record in badgeRecords)
                    {
                        if (record.Badge == null || record.Badge.Id <= 0)
                        {
                            record.Badge = _localDbService.GetBadgeDefinitionById(record.Badge?.Id ?? 0);
                        }

                        if (record.Actor == null)
                        {
                            record.Actor = _localDbService.GetActorByFilter($"Uri = \"{record.IssuedBy}\"") ?? new Actor();
                        }

                        var openBadge = _openBadgeService.GetOpenBadgeObject(record);
                        openBadges.Add(openBadge);
                    }

                    var response = new
                    {
                        _context = "https://w3id.org/openbadges/v2",
                        type = "Collection",
                        name = $"Badges for {decodedRecipient}",
                        description = "Public badges issued to this recipient",
                        totalItems = openBadges.Count,
                        items = openBadges
                    };

                    var options = new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    };

                    var json = JsonSerializer.Serialize(response, options);
                    json = json.Replace("\"_context\":", "\"@context\":");

                    Response.Headers.Append("Access-Control-Allow-Origin", "*");
                    Response.Headers.Append("Access-Control-Allow-Methods", "GET");
                    Response.Headers.Append("Access-Control-Allow-Headers", "Content-Type");
                    
                    return Content(json, "application/json");
                }
                else
                {
                    // Simple format for widget display
                    var simpleBadges = badgeRecords.Select(record => new
                    {
                        id = record.NoteId?.Split('/').LastOrDefault() ?? record.Id.ToString(),
                        title = record.Title,
                        description = record.Description,
                        image = record.FullImageUrl,
                        imageAlt = record.ImageAltText,
                        issuedBy = record.IssuedBy,
                        issuerName = record.Actor?.FullName ?? $"{record.IssuedBy.Split('/').Last()}@{new Uri(record.IssuedBy).Host}",
                        issuedOn = record.IssuedOn.ToString("yyyy-MM-dd"),
                        viewUrl = record.IsExternal 
                            ? $"/view/grant/{record.NoteId?.Split('/').LastOrDefault()}"
                            : record.NoteId,
                        isExternal = record.IsExternal
                    }).ToList();

                    Response.Headers.Append("Access-Control-Allow-Origin", "*");
                    Response.Headers.Append("Access-Control-Allow-Methods", "GET");
                    Response.Headers.Append("Access-Control-Allow-Headers", "Content-Type");

                    return new JsonResult(simpleBadges);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving badges for recipient: {Recipient}", recipient);
                return StatusCode(500, new { error = "An error occurred while retrieving badges", details = ex.Message });
            }
        }

        [HttpGet("widget.js")]
        public IActionResult GetWidget()
        {
            _logger.LogInformation("[{RequestHost}] Generating widget JavaScript", Request.Host);
            
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var widgetJs = GetWidgetJavaScript(baseUrl);
            
            Response.Headers.Append("Access-Control-Allow-Origin", "*");
            Response.Headers.Append("Access-Control-Allow-Methods", "GET");
            Response.Headers.Append("Access-Control-Allow-Headers", "Content-Type");
            Response.Headers.Append("Cache-Control", "public, max-age=3600");
            
            _logger.LogInformation("[{RequestHost}] Widget JavaScript generated successfully", Request.Host);
            return Content(widgetJs, "application/javascript");
        }

        [HttpGet("test")]
        public IActionResult TestEndpoint()
        {
            try
            {
                _logger.LogInformation("[{RequestHost}] Test endpoint accessed", Request.Host);
                
                var baseUrl = $"{Request.Scheme}://{Request.Host}";
                var testData = new
                {
                    message = "BadgeFed Embed API is working!",
                    timestamp = DateTime.UtcNow,
                    baseUrl = baseUrl,
                    endpoints = new
                    {
                        badges = $"{baseUrl}/api/embed/badges?recipient=PROFILE_URL&limit=10&format=simple",
                        widget = $"{baseUrl}/api/embed/widget.js",
                        staticWidget = $"{baseUrl}/js/widget.js"
                    }
                };

                Response.Headers.Append("Access-Control-Allow-Origin", "*");
                _logger.LogInformation("[{RequestHost}] Test endpoint executed successfully", Request.Host);
                return new JsonResult(testData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in test endpoint");
                return StatusCode(500, new { error = "Test endpoint failed", details = ex.Message });
            }
        }

        private string GetWidgetJavaScript(string baseUrl)
        {
            return $@"
(function() {{
    'use strict';
    
    // BadgeFed Widget Configuration
    const BADGEFED_API_BASE = '{baseUrl}/api/embed';
    
    // Default styles
    const DEFAULT_STYLES = `
        .badgefed-widget {{
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Oxygen, Ubuntu, Cantarell, sans-serif;
            background: #ffffff;
            border: 1px solid #e1e8ed;
            border-radius: 8px;
            padding: 20px;
            margin: 20px 0;
            box-shadow: 0 2px 8px rgba(0,0,0,0.1);
            max-width: 100%;
        }}
        .badgefed-widget h3 {{
            margin: 0 0 16px 0;
            color: #1a202c;
            font-size: 18px;
            font-weight: 600;
        }}
        .badgefed-badges {{
            display: grid;
            grid-template-columns: repeat(auto-fill, minmax(250px, 1fr));
            gap: 16px;
        }}
        .badgefed-badge {{
            border: 1px solid #e2e8f0;
            border-radius: 6px;
            padding: 16px;
            background: #f8fafc;
            transition: transform 0.2s, box-shadow 0.2s;
        }}
        .badgefed-badge:hover {{
            transform: translateY(-2px);
            box-shadow: 0 4px 12px rgba(0,0,0,0.15);
        }}
        .badgefed-badge-image {{
            width: 64px;
            height: 64px;
            border-radius: 50%;
            object-fit: cover;
            margin-bottom: 12px;
        }}
        .badgefed-badge-title {{
            font-weight: 600;
            font-size: 16px;
            color: #2d3748;
            margin-bottom: 8px;
            line-height: 1.2;
        }}
        .badgefed-badge-issuer {{
            font-size: 14px;
            color: #4a5568;
            margin-bottom: 8px;
        }}
        .badgefed-badge-date {{
            font-size: 12px;
            color: #718096;
            margin-bottom: 12px;
        }}
        .badgefed-badge-link {{
            display: inline-block;
            padding: 6px 12px;
            background: #3182ce;
            color: white;
            text-decoration: none;
            border-radius: 4px;
            font-size: 12px;
            font-weight: 500;
            transition: background-color 0.2s;
        }}
        .badgefed-badge-link:hover {{
            background: #2c5aa0;
        }}
        .badgefed-loading {{
            text-align: center;
            padding: 40px;
            color: #718096;
        }}
        .badgefed-error {{
            text-align: center;
            padding: 20px;
            color: #e53e3e;
            background: #fed7d7;
            border-radius: 4px;
        }}
        .badgefed-spinner {{
            border: 2px solid #e2e8f0;
            border-top: 2px solid #3182ce;
            border-radius: 50%;
            width: 24px;
            height: 24px;
            animation: badgefed-spin 1s linear infinite;
            margin: 0 auto 12px;
        }}
        @keyframes badgefed-spin {{
            0% {{ transform: rotate(0deg); }}
            100% {{ transform: rotate(360deg); }}
        }}
        .badgefed-powered-by {{
            margin-top: 16px;
            text-align: center;
            font-size: 12px;
            color: #718096;
        }}
        .badgefed-powered-by a {{
            color: #3182ce;
            text-decoration: none;
        }}
        .badgefed-powered-by a:hover {{
            text-decoration: underline;
        }}
    `;
    
    // Inject styles
    function injectStyles() {{
        if (document.getElementById('badgefed-styles')) return;
        
        const style = document.createElement('style');
        style.id = 'badgefed-styles';
        style.textContent = DEFAULT_STYLES;
        document.head.appendChild(style);
    }}
    
    // Format date
    function formatDate(dateStr) {{
        const date = new Date(dateStr);
        return date.toLocaleDateString('en-US', {{
            year: 'numeric',
            month: 'short',
            day: 'numeric'
        }});
    }}
    
    // Create badge HTML
    function createBadgeHTML(badge) {{
        const viewUrl = badge.viewUrl.startsWith('http') 
            ? badge.viewUrl 
            : `{baseUrl}${{badge.viewUrl}}`;
            
        return `
            <div class=""badgefed-badge"">
                <img src=""${{badge.image}}"" alt=""${{badge.imageAlt || badge.title}}"" class=""badgefed-badge-image"" />
                <div class=""badgefed-badge-title"">${{badge.title}}</div>
                <div class=""badgefed-badge-issuer"">Issued by: ${{badge.issuerName}}</div>
                <div class=""badgefed-badge-date"">Issued: ${{formatDate(badge.issuedOn)}}</div>
                <a href=""${{viewUrl}}"" target=""_blank"" class=""badgefed-badge-link"">View Badge</a>
            </div>
        `;
    }}
    
    // Main widget function
    function renderBadgeWidget(containerId, recipient, options = {{}}) {{
        const container = document.getElementById(containerId);
        if (!container) {{
            console.error('BadgeFed Widget: Container not found:', containerId);
            return;
        }}
        
        const title = options.title || 'My Badges';
        const limit = options.limit || 10;
        const showPoweredBy = options.showPoweredBy !== false;
        
        // Show loading state
        container.innerHTML = `
            <div class=""badgefed-widget"">
                <h3>${{title}}</h3>
                <div class=""badgefed-loading"">
                    <div class=""badgefed-spinner""></div>
                    Loading badges...
                </div>
                ${{showPoweredBy ? '<div class=""badgefed-powered-by"">Powered by <a href=""{baseUrl}"" target=""_blank"">BadgeFed</a></div>' : ''}}
            </div>
        `;
        
        // Fetch badges
        const apiUrl = `${{BADGEFED_API_BASE}}/badges?recipient=${{encodeURIComponent(recipient)}}&limit=${{limit}}&format=simple`;
        
        fetch(apiUrl)
            .then(response => {{
                if (!response.ok) {{
                    throw new Error(`HTTP ${{response.status}}: ${{response.statusText}}`);
                }}
                return response.json();
            }})
            .then(badges => {{
                if (!badges || badges.length === 0) {{
                    container.innerHTML = `
                        <div class=""badgefed-widget"">
                            <h3>${{title}}</h3>
                            <div class=""badgefed-error"">No public badges found for this recipient.</div>
                            ${{showPoweredBy ? '<div class=""badgefed-powered-by"">Powered by <a href=""{baseUrl}"" target=""_blank"">BadgeFed</a></div>' : ''}}
                        </div>
                    `;
                    return;
                }}
                
                const badgesHTML = badges.map(createBadgeHTML).join('');
                container.innerHTML = `
                    <div class=""badgefed-widget"">
                        <h3>${{title}}</h3>
                        <div class=""badgefed-badges"">
                            ${{badgesHTML}}
                        </div>
                        ${{showPoweredBy ? '<div class=""badgefed-powered-by"">Powered by <a href=""{baseUrl}"" target=""_blank"">BadgeFed</a></div>' : ''}}
                    </div>
                `;
            }})
            .catch(error => {{
                console.error('BadgeFed Widget Error:', error);
                container.innerHTML = `
                    <div class=""badgefed-widget"">
                        <h3>${{title}}</h3>
                        <div class=""badgefed-error"">Failed to load badges. Please try again later.</div>
                        ${{showPoweredBy ? '<div class=""badgefed-powered-by"">Powered by <a href=""{baseUrl}"" target=""_blank"">BadgeFed</a></div>' : ''}}
                    </div>
                `;
            }});
    }}
    
    // Initialize
    injectStyles();
    
    // Expose to global scope
    window.BadgeFedWidget = {{
        render: renderBadgeWidget
    }};
    
    // Auto-initialize widgets with data attributes
    document.addEventListener('DOMContentLoaded', function() {{
        const widgets = document.querySelectorAll('[data-badgefed-recipient]');
        widgets.forEach(function(widget) {{
            const recipient = widget.getAttribute('data-badgefed-recipient');
            const title = widget.getAttribute('data-badgefed-title') || 'My Badges';
            const limit = parseInt(widget.getAttribute('data-badgefed-limit')) || 10;
            const showPoweredBy = widget.getAttribute('data-badgefed-powered-by') !== 'false';
            
            if (recipient && widget.id) {{
                renderBadgeWidget(widget.id, recipient, {{ title, limit, showPoweredBy }});
            }}
        }});
    }});
}})();
";
        }
    }
}
