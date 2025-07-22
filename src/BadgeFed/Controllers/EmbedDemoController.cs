using Microsoft.AspNetCore.Mvc;

namespace BadgeFed.Controllers
{
    [Route("embed")]
    public class EmbedDemoController : Controller
    {
        [HttpGet("demo")]
        public IActionResult Demo()
        {
            return PhysicalFile(
                Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "embed-demo.html"),
                "text/html");
        }
    }
}
