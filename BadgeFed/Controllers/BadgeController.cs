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

            if (accept.Contains("application/json") || accept.Contains("application/activity"))
            {
                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "badges", $"{id}.json");
                
                if (!System.IO.File.Exists(filePath))
                {
                    return NotFound("Badge not found");
                }
                
                var json = System.IO.File.ReadAllText(filePath);
                return Content(json, "application/activity+json");
            }

            return Redirect($"/view/badge/{id}");
        }
    }
}