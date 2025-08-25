using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using BadgeFed.Services;
using BadgeFed.Models;
using ActivityPubDotNet.Core;

namespace BadgeFed.Controllers
{
    [ApiController]
    public class OutboxController : ControllerBase
    {
        private readonly ILogger<OutboxController> _logger;
        private readonly LocalScopedDb _localDbService;
        private readonly BadgeService _badgeService;

        public OutboxController(
            ILogger<OutboxController> logger,
            LocalScopedDb localDbService,
            BadgeService badgeService)
        {
            _logger = logger;
            _localDbService = localDbService;
            _badgeService = badgeService;
        }

        [HttpGet]
        [Route("outbox")]
        public IActionResult GetDomainOutbox([FromQuery] bool page = false, [FromQuery] string? maxId = null, [FromQuery] string? minId = null)
        {
            var accept = Request.Headers["Accept"].ToString();

            if (!BadgeFed.Core.ActivityPubHelper.IsActivityPubRequest(accept))
            {
                return BadRequest("This endpoint only supports ActivityPub requests");
            }

            var domain = Request.Host.Host;
            var baseOutboxUrl = $"https://{domain}/outbox";

            if (!page)
            {
                // Return the main OrderedCollection for the entire domain
                var totalItems = GetTotalGrantsForDomain();
                
                var outboxCollection = new ActivityPubCollection
                {
                    Context = "https://www.w3.org/ns/activitystreams",
                    Id = baseOutboxUrl,
                    Type = "OrderedCollection", 
                    TotalItems = totalItems
                };

                if (totalItems > 0)
                {
                    outboxCollection.OrderedItems = new List<dynamic> 
                    { 
                        $"{baseOutboxUrl}?page=true" 
                    };
                }

                return new JsonResult(outboxCollection)
                {
                    ContentType = "application/activity+json"
                };
            }
            else
            {
                // Return a page of ordered items for the entire domain
                const int pageSize = 20;
                var grants = GetGrantsForDomain(maxId, minId, pageSize);
                
                var orderedItems = new List<object>();
                
                foreach (var grant in grants)
                {
                    var actor = grant.Actor ?? _localDbService.GetActorByUri(grant.IssuedBy);
                    if (actor != null)
                    {
                        var createActivity = CreateBadgeGrantActivity(grant, actor);
                        orderedItems.Add(createActivity);
                    }
                }

                var collectionPage = new ActivityPubOrderedCollectionPage
                {
                    Context = "https://www.w3.org/ns/activitystreams",
                    Id = BuildPageUrl(baseOutboxUrl, maxId, minId),
                    Type = "OrderedCollectionPage",
                    PartOf = baseOutboxUrl,
                    OrderedItems = orderedItems
                };

                // Set pagination links
                if (grants.Count > 0)
                {
                    var firstGrant = grants.First();
                    var lastGrant = grants.Last();
                    
                    // Check if there are newer items
                    var hasNewer = HasNewerGrantsForDomain(firstGrant.Id);
                    if (hasNewer)
                    {
                        collectionPage.Prev = $"{baseOutboxUrl}?page=true&min_id={firstGrant.Id}";
                    }
                    
                    // Check if there are older items
                    var hasOlder = HasOlderGrantsForDomain(lastGrant.Id);
                    if (hasOlder)
                    {
                        collectionPage.Next = $"{baseOutboxUrl}?page=true&max_id={lastGrant.Id}";
                    }
                }

                return new JsonResult(collectionPage)
                {
                    ContentType = "application/activity+json"
                };
            }
        }

        [HttpGet]
        [Route("actors/{domain}/{actorName}/outbox")]
        public IActionResult GetActorOutbox(string domain, string actorName, [FromQuery] bool page = false, [FromQuery] string? maxId = null, [FromQuery] string? minId = null)
        {
            var accept = Request.Headers["Accept"].ToString();

            if (!BadgeFed.Core.ActivityPubHelper.IsActivityPubRequest(accept))
            {
                return BadRequest("This endpoint only supports ActivityPub requests");
            }

            var actor = _localDbService.GetActorByFilter($"Username = \"{actorName}\" AND Domain = \"{domain}\"");

            if (actor == null)
            {
                return NotFound("Actor not found on this domain");
            }

            var baseOutboxUrl = $"https://{domain}/actors/{domain}/{actorName}/outbox";

            if (!page)
            {
                // Return the main OrderedCollection
                var totalItems = GetTotalGrantsForActor(actor.Uri.ToString());
                
                var outboxCollection = new ActivityPubCollection
                {
                    Context = "https://www.w3.org/ns/activitystreams",
                    Id = baseOutboxUrl,
                    Type = "OrderedCollection", 
                    TotalItems = totalItems
                };

                if (totalItems > 0)
                {
                    outboxCollection.OrderedItems = new List<dynamic> 
                    { 
                        $"{baseOutboxUrl}?page=true" 
                    };
                }

                return new JsonResult(outboxCollection)
                {
                    ContentType = "application/activity+json"
                };
            }
            else
            {
                // Return a page of ordered items
                const int pageSize = 20;
                var grants = GetGrantsForActor(actor.Uri.ToString(), maxId, minId, pageSize);
                
                var orderedItems = new List<object>();
                
                foreach (var grant in grants)
                {
                    var createActivity = CreateBadgeGrantActivity(grant, actor);
                    orderedItems.Add(createActivity);
                }

                var collectionPage = new ActivityPubOrderedCollectionPage
                {
                    Context = "https://www.w3.org/ns/activitystreams",
                    Id = BuildPageUrl(baseOutboxUrl, maxId, minId),
                    Type = "OrderedCollectionPage",
                    PartOf = baseOutboxUrl,
                    OrderedItems = orderedItems
                };

                // Set pagination links
                if (grants.Count > 0)
                {
                    var firstGrant = grants.First();
                    var lastGrant = grants.Last();
                    
                    // Check if there are newer items
                    var hasNewer = HasNewerGrants(actor.Uri.ToString(), firstGrant.Id);
                    if (hasNewer)
                    {
                        collectionPage.Prev = $"{baseOutboxUrl}?page=true&min_id={firstGrant.Id}";
                    }
                    
                    // Check if there are older items
                    var hasOlder = HasOlderGrants(actor.Uri.ToString(), lastGrant.Id);
                    if (hasOlder)
                    {
                        collectionPage.Next = $"{baseOutboxUrl}?page=true&max_id={lastGrant.Id}";
                    }
                }

                return new JsonResult(collectionPage)
                {
                    ContentType = "application/activity+json"
                };
            }
        }

        private int GetTotalGrantsForActor(string actorUri)
        {
            var filter = new LocalScopedDb.Filter
            {
                Where = "IssuedBy = @ActorUri AND FingerPrint IS NOT NULL AND FingerPrint != ''",
                Parameters = { { "ActorUri", actorUri } }
            };

            var grants = _localDbService.GetBadgeRecords(filter);
            return grants.Count;
        }

        private List<BadgeRecord> GetGrantsForActor(string actorUri, string? maxId, string? minId, int limit)
        {
            var whereClause = "IssuedBy = @ActorUri AND FingerPrint IS NOT NULL AND FingerPrint != ''";
            var parameters = new Dictionary<string, object> { { "ActorUri", actorUri } };

            if (!string.IsNullOrEmpty(maxId))
            {
                whereClause += " AND Id < @MaxId";
                parameters.Add("MaxId", long.Parse(maxId));
            }

            if (!string.IsNullOrEmpty(minId))
            {
                whereClause += " AND Id > @MinId";
                parameters.Add("MinId", long.Parse(minId));
            }

            var filter = new LocalScopedDb.Filter
            {
                Where = whereClause,
                Parameters = parameters
            };

            var allGrants = _localDbService.GetBadgeRecords(filter, true);
            
            // Sort by Id descending (newest first) and take the requested limit
            var grants = allGrants
                .OrderByDescending(g => g.Id)
                .Take(limit)
                .ToList();
            
            // Populate actor information for each grant
            foreach (var grant in grants)
            {
                if (grant.Actor == null)
                {
                    grant.Actor = _localDbService.GetActorByUri(grant.IssuedBy);
                }
            }

            return grants;
        }

        private bool HasNewerGrants(string actorUri, long afterId)
        {
            var filter = new LocalScopedDb.Filter
            {
                Where = "IssuedBy = @ActorUri AND FingerPrint IS NOT NULL AND FingerPrint != '' AND Id > @AfterId",
                Parameters = { { "ActorUri", actorUri }, { "AfterId", afterId } }
            };

            var grants = _localDbService.GetBadgeRecords(filter);
            return grants.Count > 0;
        }

        private bool HasOlderGrants(string actorUri, long beforeId)
        {
            var filter = new LocalScopedDb.Filter
            {
                Where = "IssuedBy = @ActorUri AND FingerPrint IS NOT NULL AND FingerPrint != '' AND Id < @BeforeId",
                Parameters = { { "ActorUri", actorUri }, { "BeforeId", beforeId } }
            };

            var grants = _localDbService.GetBadgeRecords(filter);
            return grants.Count > 0;
        }

        private string BuildPageUrl(string baseUrl, string? maxId, string? minId)
        {
            var url = $"{baseUrl}?page=true";
            
            if (!string.IsNullOrEmpty(maxId))
            {
                url += $"&max_id={maxId}";
            }
            
            if (!string.IsNullOrEmpty(minId))
            {
                url += $"&min_id={minId}";
            }
            
            return url;
        }

        private object CreateBadgeGrantActivity(BadgeRecord grant, Actor actor)
        {
            // Create a Create activity for the badge grant
            var activityId = $"{grant.NoteId}/activity";
            
            // Get the badge note
            var badgeNote = _badgeService.GetNoteFromBadgeRecord(grant);
            
            var createActivity = new
            {
                id = activityId,
                type = "Create",
                actor = actor.Uri.ToString(),
                published = grant.IssuedOn.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                to = new[] { "https://www.w3.org/ns/activitystreams#Public" },
                cc = new[] { $"{actor.Uri}/followers", grant.IssuedToSubjectUri },
                @object = badgeNote
            };

            return createActivity;
        }

        private int GetTotalGrantsForDomain()
        {
            var filter = new LocalScopedDb.Filter
            {
                Where = "FingerPrint IS NOT NULL AND FingerPrint != ''"
            };

            var grants = _localDbService.GetBadgeRecords(filter);
            return grants.Count;
        }

        private List<BadgeRecord> GetGrantsForDomain(string? maxId, string? minId, int limit)
        {
            var whereClause = "FingerPrint IS NOT NULL AND FingerPrint != ''";
            var parameters = new Dictionary<string, object>();

            if (!string.IsNullOrEmpty(maxId))
            {
                whereClause += " AND Id < @MaxId";
                parameters.Add("MaxId", long.Parse(maxId));
            }

            if (!string.IsNullOrEmpty(minId))
            {
                whereClause += " AND Id > @MinId";
                parameters.Add("MinId", long.Parse(minId));
            }

            var filter = new LocalScopedDb.Filter
            {
                Where = whereClause,
                Parameters = parameters
            };

            var allGrants = _localDbService.GetBadgeRecords(filter, true);
            
            // Sort by Id descending (newest first) and take the requested limit
            var grants = allGrants
                .OrderByDescending(g => g.Id)
                .Take(limit)
                .ToList();
            
            // Populate actor information for each grant
            foreach (var grant in grants)
            {
                if (grant.Actor == null)
                {
                    grant.Actor = _localDbService.GetActorByUri(grant.IssuedBy);
                }
            }

            return grants;
        }

        private bool HasNewerGrantsForDomain(long afterId)
        {
            var filter = new LocalScopedDb.Filter
            {
                Where = "FingerPrint IS NOT NULL AND FingerPrint != '' AND Id > @AfterId",
                Parameters = { { "AfterId", afterId } }
            };

            var grants = _localDbService.GetBadgeRecords(filter);
            return grants.Count > 0;
        }

        private bool HasOlderGrantsForDomain(long beforeId)
        {
            var filter = new LocalScopedDb.Filter
            {
                Where = "FingerPrint IS NOT NULL AND FingerPrint != '' AND Id < @BeforeId",
                Parameters = { { "BeforeId", beforeId } }
            };

            var grants = _localDbService.GetBadgeRecords(filter);
            return grants.Count > 0;
        }
    }
}
