using Microsoft.AspNetCore.Mvc;

namespace BadgeFed.Controllers
{
    [ApiController]
    [Route("badge")]
    public class BadgeController : ControllerBase
    {
        [HttpGet("{id}")]
        public IActionResult GetBadge(string id)
        {
            var accept = Request.Headers["Accept"].ToString();

            if (accept.Contains("application/json"))
            {
                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "badges", $"{id}.json");
                if (System.IO.File.Exists(filePath))
                {
                    var json = System.IO.File.ReadAllText(filePath);
                    return Content(json, "application/json");
                }
            }

            return Redirect($"/badge/verify/{id}");
        }
    }
}