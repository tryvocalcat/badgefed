using ActivityPubDotNet.Core;
using BadgeFed.Services;
using Microsoft.AspNetCore.Mvc;

namespace BadgeFed.Controllers
{
    [ApiController]
    [Route("actors/{domain}/{actorName}")]
    public class ActorController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly LocalScopedDb _localDbService;

        public ActorController(IConfiguration configuration, LocalScopedDb localDbService)
        {
            _configuration = configuration;
            _localDbService = localDbService;
        }

        [HttpGet]
        public IActionResult GetActor(string domain, string actorName)
        {
            var accept = Request.Headers["Accept"].ToString();

            if (!BadgeFed.Core.ActivityPubHelper.IsActivityPubRequest(accept))
            {
                return Redirect($"/view/actor/{domain}/{actorName}");
            }

            var actor = _localDbService.GetActorByFilter($"Username = \"{actorName}\" AND Domain = \"{domain}\"");

            if (actor == null)
            {
                return NotFound("Account not found on this domain");
            }

            var baseUrlId = $"https://{domain}/actors/{domain}/{actorName}";

            var actorResource = new ActivityPubActor
            {
                Context = "https://www.w3.org/ns/activitystreams",
                Id = $"{baseUrlId}",
                Type = "Service",
                Following = $"{baseUrlId}/following",
                Followers = $"{baseUrlId}/followers",
                Inbox = $"https://{domain}/inbox",
                Outbox = $"https://{domain}/outbox",
                PreferredUsername = actorName,
                Name = actor.FullName,
                Summary = actor.Summary,
                Url = actor.InformationUri!,
                Discoverable = true,
                Memorial = false,
                Icon = new ActivityPubImage
                {
                    Type = "Image",
                    MediaType = "image/png",
                    Url = $"https://{domain}/{actor.AvatarPath}"
                },
                Image = new ActivityPubImage
                {
                    Type = "Image",
                    MediaType = "image/png",
                    Url = $"https://{domain}/{actor.AvatarPath}"
                },
                PublicKey = new ActivityPubActor.PublicKeyDefinition
                {
                    Id = actor.KeyId,
                    Owner = baseUrlId,
                    PublicKeyPem = actor.PublicKeyPemClean!
                }
            };

            return new JsonResult(actorResource)
            {
                ContentType = "application/activity+json"
            };
        }

        [HttpGet]
        [Route("followers")]
        public IActionResult GetFollowers(string domain, string actorName)
        {
            var actor = _localDbService.GetActorByFilter($"Username = \"{actorName}\" AND Domain = \"{domain}\"");

            if (actor == null)
            {
                return NotFound("Account not found on this domain");
            }

            var followers = _localDbService.GetFollowersByActorId(actor.Id);

            var orderedItems = followers.Select(follower => follower.FollowerUri).ToList();

            var orderedItemsObjects = orderedItems.Cast<dynamic>().ToList();

            var followersCollection = new ActivityPubCollection
            {
                Context = "https://www.w3.org/ns/activitystreams",
                Id = $"https://{domain}/actors/{domain}/{actorName}/followers",
                Type = "Collection",
                TotalItems = followers.Count,
                OrderedItems = orderedItemsObjects
            };

            return new JsonResult(followersCollection)
            {
                ContentType = "application/activity+json"
            };
        }
    }
}