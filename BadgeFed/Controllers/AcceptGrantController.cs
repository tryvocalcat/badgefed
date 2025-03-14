using Microsoft.AspNetCore.Mvc;

namespace BadgeFed.Controllers
{
    [ApiController]
    [Route("accept")]
    public class BadgeController : ControllerBase
    {
        private LocalDbService DbService { get; }

        public BadgeController(LocalDbService dbService)
        {
            DbService = dbService;
        }

        [HttpGet("{id}/{key}")]
        public IActionResult AcceptBadge()
        {
            DbService.AcceptBadge();

            return Ok();
        }
    }
}