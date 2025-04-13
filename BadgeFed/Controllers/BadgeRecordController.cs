using Microsoft.AspNetCore.Mvc;

namespace BadgeFed.Controllers
{
    [ApiController]
    [Route("grant")]
    public class BadgeController : ControllerBase
    {
        [HttpGet("{noteId}")]
        public IActionResult GetBadge(string noteId)
        {
            var accept = Request.Headers["Accept"].ToString();

            if (!BadgeFed.Core.ActivityPubHelper.IsActivityPubRequest(accept))
            {
                return Redirect($"/view/grant/{noteId}");
            }
            
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "badges", $"{noteId}.json");
            
            if (!System.IO.File.Exists(filePath))
            {
                return NotFound("Badge record not found");
            }
            
            var json = System.IO.File.ReadAllText(filePath);
            
            return Content(json, "application/activity+json");
        }
    }
}