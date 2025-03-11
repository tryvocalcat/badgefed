using BadgeFed.Services;
using Microsoft.AspNetCore.Mvc;

namespace BadgeFed.Controllers
{
    [ApiController]
    [Route("actors/{domain}/{id}")]
    public class ActorController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly LocalDbService _localDbService;

        public ActorController(IConfiguration configuration, LocalDbService localDbService)
        {
            _configuration = configuration;
            _localDbService = localDbService;
        }

        [HttpGet]
        public IActionResult GetActor(string domain, string actorName)
        {
            var actor = _localDbService.GetActorByFilter($"Username = \"{actorName}\" AND Domain = \"{domain}\"");

            if (actor == null)
            {
                return NotFound("Account not found on this domain");
            }

            var baseUrlId = $"https://{domain}/actors/{domain}/{actorName}";

            var actorResource = new
            {
                @context = "https://www.w3.org/ns/activitystreams",
                id = $"{baseUrlId}",
                type = "Person",
                following = $"{baseUrlId}/following",
                followers = $"{baseUrlId}/followers",
                inbox = $"https://{domain}/inbox",
                outbox = $"https://{domain}/outbox",
                preferredUsername = actorName,
                name = actor.FullName,
                summary = actor.Summary,
                url = actor.InformationUri,
                discoverable = true,
                memorial = false,
                icon = new
                {
                    type = "Image",
                    mediaType = "image/png",
                    url = $"https://{domain}/{actor.AvatarPath}"
                },
                image = new
                {
                    type = "Image",
                    mediaType = "image/png",
                    url = $"https://{domain}/{actor.AvatarPath}"
                },
                publicKey = new
                {
                    @context = "https://w3id.org/security/v1",
                    @type = "Key",
                    id = $"{baseUrlId}#main-key",
                    owner = baseUrlId,
                    publicKeyPem = actor.PublicKeyPem
                },
                attachment = new[]
                {
                    new
                    {
                        type = "PropertyValue",
                        name = "Me",
                        value = $"<a href=\"{actor.InformationUri}\" target=\"_blank\" rel=\"nofollow noopener noreferrer me\" translate=\"no\"><span class=\"invisible\">https://</span><span class=\"\">{actor.InformationUri}</span><span class=\"invisible\"></span></a>"
                    }
                }
            };

            return Ok(actorResource);
        }
    }
}