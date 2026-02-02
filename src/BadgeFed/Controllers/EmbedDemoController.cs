using Microsoft.AspNetCore.Mvc;

namespace BadgeFed.Controllers
{
    [Route("embed")]
    public class EmbedDemoController : Controller
    {
        private readonly ILogger<EmbedDemoController> _logger;

        public EmbedDemoController(ILogger<EmbedDemoController> logger)
        {
            _logger = logger;
        }
        [HttpGet("demo")]
        public IActionResult Demo()
        {
            _logger.LogInformation("[{RequestHost}] Serving embed demo page", Request.Host);
            return PhysicalFile(
                Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "embed-demo.html"),
                "text/html");
        }
    }
}
