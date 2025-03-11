using Microsoft.AspNetCore.Mvc;

namespace BadgeFed.Controllers
{
    [ApiController]
    [Route("actors/{id}")]
    public class ActorController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public ActorController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet]
        public IActionResult GetActor(string id)
        {
            var domain = _configuration["Domain"];
            var actorName = _configuration["ActorName"];
            var actorSummary = _configuration["ActorSummary"];

            var actor = new
            {
                @context = "https://www.w3.org/ns/activitystreams",
                id = $"https://{domain}/actors/{id}",
                type = "Person",
                following = $"https://{domain}/actors/{id}/following",
                followers = $"https://{domain}/actors/{id}/followers",
                inbox = $"https://{domain}/inbox",
                outbox = $"https://{domain}/outbox",
                preferredUsername = id,
                name = actorName,
                summary = actorSummary,
                url = $"https://{domain}/",
                discoverable = true,
                memorial = false,
                icon = new
                {
                    type = "Image",
                    mediaType = "image/png",
                    url = $"https://{domain}/img/avatar.png"
                },
                image = new
                {
                    type = "Image",
                    mediaType = "image/png",
                    url = $"https://{domain}/img/avatar.png"
                },
                publicKey = new
                {
                    @context = "https://w3id.org/security/v1",
                    @type = "Key",
                    id = $"https://{domain}/@{id}#main-key",
                    owner = $"https://{domain}/@{id}",
                    publicKeyPem = "-----BEGIN PUBLIC KEY-----\nMIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA68oSTjzLryZ+lLIu8N5+\nCZdQPKaN6xZCY93uzJ8b4wjOecEykQcGU2J+ejOzMXHP4o4N+Rc0xnxyAs9ZN5AX\ndYSObpdfGQvrvdHanu+iTyRKETKMbSHtJzk5dZW8l+pPnX2YWKVgSfCG2SALZprg\nzxyhbtTLq8JoN8b5TgEA1B12Rya3aBNNXDT1/eeU+/HqwtKN2nLAdvACbccPAtg1\nVeKdcSgmS2o51JR4MjJWcCgM2HrAZUepF1XM59Yeq136QGviJpfAFX6gS7POvi7r\n3iaH0GzuUzR+WJSHgoJ65VzC9wy4Vpw/jt8CNtlW13iFRasHARTwFe+1FhuZayPG\neQIDAQAB\n-----END PUBLIC KEY-----"
                },
                attachment = new[]
                {
                    new
                    {
                        type = "PropertyValue",
                        name = "Blog",
                        value = $"<a href=\"https://{domain}\" target=\"_blank\" rel=\"nofollow noopener noreferrer me\" translate=\"no\"><span class=\"invisible\">https://</span><span class=\"\">{domain}</span><span class=\"invisible\"></span></a>"
                    },
                    new
                    {
                        type = "PropertyValue",
                        name = "GitHub",
                        value = "<a href=\"https://github.com/tryvocalcat/activitypub-badges\" target=\"_blank\" rel=\"nofollow noopener noreferrer me\" translate=\"no\"><span class=\"invisible\">https://</span><span class=\"\">github.com/tryvocalcat/activitypub-badges</span><span class=\"invisible\"></span></a>"
                    }
                }
            };

            return Ok(actor);
        }
    }
}