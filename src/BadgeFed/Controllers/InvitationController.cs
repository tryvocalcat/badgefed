using Microsoft.AspNetCore.Mvc;

namespace BadgeFed.Controllers
{
    [ApiController]
    [Route("invitation")]
    public class InvitationController : ControllerBase
    {
        private readonly ILogger<InvitationController> _logger;

        public InvitationController(ILogger<InvitationController> logger)
        {
            _logger = logger;
        }
        
        // TODO: Add invitation-related endpoints here
        // This controller was empty and needs implementation
    }
}