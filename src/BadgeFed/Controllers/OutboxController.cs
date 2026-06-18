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
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly FederationAnalyticsService _analytics;

        public OutboxController(
            ILogger<OutboxController> logger,
            LocalScopedDb localDbService,
            BadgeService badgeService,
            IHttpClientFactory httpClientFactory,
            FederationAnalyticsService analytics)
        {
            _logger = logger;
            _localDbService = localDbService;
            _badgeService = badgeService;
            _httpClientFactory = httpClientFactory;
            _analytics = analytics;
        }

        [HttpGet]
        [Route("outbox")]
        public async Task<IActionResult> GetDomainOutbox([FromQuery] bool page = false, [FromQuery] string? maxId = null, [FromQuery] string? minId = null, [FromQuery] string? attributedTo = null)
        {
            _logger.LogInformation("[{RequestHost}] Domain outbox request - page: {Page}, maxId: {MaxId}, minId: {MinId}, attributedTo: {AttributedTo}", Request.Host, page, maxId, minId, attributedTo);
            
            var accept = Request.Headers["Accept"].ToString();

            if (!BadgeFed.Core.ActivityPubHelper.IsActivityPubRequest(accept))
            {
                _logger.LogWarning("[{RequestHost}] Non-ActivityPub request rejected for domain outbox", Request.Host);
                return BadRequest("This endpoint only supports ActivityPub requests");
            }

            var recipientUris = await ResolveAttributedToUris(attributedTo);

            var domain = Request.Host.Host;
            var baseOutboxUrl = $"https://{domain}/outbox";

            _analytics.TrackEventAutoGroup(
                FederationEventType.OutboxRequested,
                actorUri: attributedTo,
                objectUri: baseOutboxUrl,
                remoteHost: Request.Headers["X-Forwarded-For"].FirstOrDefault() ?? Request.HttpContext.Connection.RemoteIpAddress?.ToString(),
                requestIp: Request.HttpContext.Connection.RemoteIpAddress?.ToString(),
                userAgent: Request.Headers["User-Agent"].ToString());

            if (!page)
            {
                // Return the main OrderedCollection for the entire domain
                var totalItems = GetTotalGrantsForDomain(recipientUris);
                
                _logger.LogInformation("[{RequestHost}] Returning domain outbox main collection with {TotalItems} items", Request.Host, totalItems);
                
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
                        BuildPageUrl(baseOutboxUrl, null, null, attributedTo) 
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
                var grants = GetGrantsForDomain(maxId, minId, pageSize, recipientUris);
                
                var orderedItems = new List<object>();
                
                foreach (var grant in grants)
                {
                    var actor = grant.Actor ?? _localDbService.GetActorByUri(grant.IssuedBy);
                    if (actor != null)
                    {
                        var createActivity = CreateBadgeGrantActivity(grant, actor);
                        if (createActivity != null)
                        {
                            orderedItems.Add(createActivity);
                        }
                    }
                }

                var collectionPage = new ActivityPubOrderedCollectionPage
                {
                    Context = "https://www.w3.org/ns/activitystreams",
                    Id = BuildPageUrl(baseOutboxUrl, maxId, minId, attributedTo),
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
                    var hasNewer = HasNewerGrantsForDomain(firstGrant.Id, recipientUris);
                    if (hasNewer)
                    {
                        collectionPage.Prev = BuildPageUrl(baseOutboxUrl, null, firstGrant.Id.ToString(), attributedTo);
                    }
                    
                    // Check if there are older items
                    var hasOlder = HasOlderGrantsForDomain(lastGrant.Id, recipientUris);
                    if (hasOlder)
                    {
                        collectionPage.Next = BuildPageUrl(baseOutboxUrl, lastGrant.Id.ToString(), null, attributedTo);
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
        public async Task<IActionResult> GetActorOutbox(string domain, string actorName, [FromQuery] bool page = false, [FromQuery] string? maxId = null, [FromQuery] string? minId = null, [FromQuery] string? attributedTo = null)
        {
            _logger.LogInformation("[{RequestHost}] Actor outbox request for {ActorName}@{Domain} - page: {Page}, maxId: {MaxId}, minId: {MinId}, attributedTo: {AttributedTo}", Request.Host, actorName, domain, page, maxId, minId, attributedTo);
            
            var accept = Request.Headers["Accept"].ToString();

            if (!BadgeFed.Core.ActivityPubHelper.IsActivityPubRequest(accept))
            {
                _logger.LogWarning("[{RequestHost}] Non-ActivityPub request rejected for actor outbox: {ActorName}@{Domain}", Request.Host, actorName, domain);
                return BadRequest("This endpoint only supports ActivityPub requests");
            }

            var recipientUris = await ResolveAttributedToUris(attributedTo);

            var actor = _localDbService.GetActorByFilter($"Username = \"{actorName}\" AND Domain = \"{domain}\"");

            if (actor == null)
            {
                _logger.LogWarning("[{RequestHost}] Actor not found for outbox request: {ActorName}@{Domain}", Request.Host, actorName, domain);
                return NotFound("Actor not found on this domain");
            }

            var baseOutboxUrl = $"https://{domain}/actors/{domain}/{actorName}/outbox";

            if (!page)
            {
                // Return the main OrderedCollection
                var totalItems = GetTotalGrantsForActor(actor.Uri.ToString(), recipientUris);
                
                _logger.LogInformation("[{RequestHost}] Returning actor outbox main collection for {ActorName}@{Domain} with {TotalItems} items", Request.Host, actorName, domain, totalItems);
                
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
                        BuildPageUrl(baseOutboxUrl, null, null, attributedTo) 
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
                var grants = GetGrantsForActor(actor.Uri.ToString(), maxId, minId, pageSize, recipientUris);
                
                var orderedItems = new List<object>();
                
                foreach (var grant in grants)
                {
                    var createActivity = CreateBadgeGrantActivity(grant, actor);
                    if (createActivity != null)
                    {
                        orderedItems.Add(createActivity);
                    }
                }

                var collectionPage = new ActivityPubOrderedCollectionPage
                {
                    Context = "https://www.w3.org/ns/activitystreams",
                    Id = BuildPageUrl(baseOutboxUrl, maxId, minId, attributedTo),
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
                    var hasNewer = HasNewerGrants(actor.Uri.ToString(), firstGrant.Id, recipientUris);
                    if (hasNewer)
                    {
                        collectionPage.Prev = BuildPageUrl(baseOutboxUrl, null, firstGrant.Id.ToString(), attributedTo);
                    }
                    
                    // Check if there are older items
                    var hasOlder = HasOlderGrants(actor.Uri.ToString(), lastGrant.Id, recipientUris);
                    if (hasOlder)
                    {
                        collectionPage.Next = BuildPageUrl(baseOutboxUrl, lastGrant.Id.ToString(), null, attributedTo);
                    }
                }
                
                _logger.LogInformation("[{RequestHost}] Returning actor outbox page for {ActorName}@{Domain} with {ItemCount} items", Request.Host, actorName, domain, collectionPage.OrderedItems.Count);

                return new JsonResult(collectionPage)
                {
                    ContentType = "application/activity+json"
                };
            }
        }

        private int GetTotalGrantsForActor(string actorUri, List<string>? recipientUris = null)
        {
            var whereClause = "IssuedBy = @ActorUri AND FingerPrint IS NOT NULL AND FingerPrint != ''";
            var parameters = new Dictionary<string, object> { { "ActorUri", actorUri } };

            AppendRecipientFilter(ref whereClause, parameters, recipientUris);

            var filter = new LocalScopedDb.Filter
            {
                Where = whereClause,
                Parameters = parameters
            };

            var grants = _localDbService.GetBadgeRecords(filter);
            return grants.Count;
        }

        private List<BadgeRecord> GetGrantsForActor(string actorUri, string? maxId, string? minId, int limit, List<string>? recipientUris = null)
        {
            var whereClause = "br.IssuedBy = @ActorUri AND FingerPrint IS NOT NULL AND FingerPrint != ''";
            var parameters = new Dictionary<string, object> { { "ActorUri", actorUri } };

            AppendRecipientFilter(ref whereClause, parameters, recipientUris, "br.");

            if (!string.IsNullOrEmpty(maxId))
            {
                whereClause += " AND br.Id < @MaxId";
                parameters.Add("MaxId", long.Parse(maxId));
            }

            if (!string.IsNullOrEmpty(minId))
            {
                whereClause += " AND br.Id > @MinId";
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

        private bool HasNewerGrants(string actorUri, long afterId, List<string>? recipientUris = null)
        {
            var whereClause = "br.IssuedBy = @ActorUri AND FingerPrint IS NOT NULL AND FingerPrint != '' AND br.Id > @AfterId";
            var parameters = new Dictionary<string, object> { { "ActorUri", actorUri }, { "AfterId", afterId } };

            AppendRecipientFilter(ref whereClause, parameters, recipientUris, "br.");

            var filter = new LocalScopedDb.Filter
            {
                Where = whereClause,
                Parameters = parameters
            };

            var grants = _localDbService.GetBadgeRecords(filter);
            return grants.Count > 0;
        }

        private bool HasOlderGrants(string actorUri, long beforeId, List<string>? recipientUris = null)
        {
            var whereClause = "br.IssuedBy = @ActorUri AND FingerPrint IS NOT NULL AND FingerPrint != '' AND br.Id < @BeforeId";
            var parameters = new Dictionary<string, object> { { "ActorUri", actorUri }, { "BeforeId", beforeId } };

            AppendRecipientFilter(ref whereClause, parameters, recipientUris, "br.");

            var filter = new LocalScopedDb.Filter
            {
                Where = whereClause,
                Parameters = parameters
            };

            var grants = _localDbService.GetBadgeRecords(filter);
            return grants.Count > 0;
        }

        private string BuildPageUrl(string baseUrl, string? maxId, string? minId, string? attributedTo = null)
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

            if (!string.IsNullOrEmpty(attributedTo))
            {
                url += $"&attributedTo={Uri.EscapeDataString(attributedTo)}";
            }
            
            return url;
        }

        private object? CreateBadgeGrantActivity(BadgeRecord grant, Actor actor)
        {
            if (actor.Uri == null || string.IsNullOrEmpty(grant.NoteId))
            {
                return null;
            }

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

        private int GetTotalGrantsForDomain(List<string>? recipientUris = null)
        {
            var whereClause = "FingerPrint IS NOT NULL AND FingerPrint != ''";
            var parameters = new Dictionary<string, object>();

            AppendRecipientFilter(ref whereClause, parameters, recipientUris);

            var filter = new LocalScopedDb.Filter
            {
                Where = whereClause,
                Parameters = parameters
            };

            var grants = _localDbService.GetBadgeRecords(filter);
            return grants.Count;
        }

        private List<BadgeRecord> GetGrantsForDomain(string? maxId, string? minId, int limit, List<string>? recipientUris = null)
        {
            var whereClause = "FingerPrint IS NOT NULL AND FingerPrint != ''";
            var parameters = new Dictionary<string, object>();

            AppendRecipientFilter(ref whereClause, parameters, recipientUris);

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

        private bool HasNewerGrantsForDomain(long afterId, List<string>? recipientUris = null)
        {
            var whereClause = "FingerPrint IS NOT NULL AND FingerPrint != '' AND Id > @AfterId";
            var parameters = new Dictionary<string, object> { { "AfterId", afterId } };

            AppendRecipientFilter(ref whereClause, parameters, recipientUris);

            var filter = new LocalScopedDb.Filter
            {
                Where = whereClause,
                Parameters = parameters
            };

            var grants = _localDbService.GetBadgeRecords(filter);
            return grants.Count > 0;
        }

        private bool HasOlderGrantsForDomain(long beforeId, List<string>? recipientUris = null)
        {
            var whereClause = "FingerPrint IS NOT NULL AND FingerPrint != '' AND Id < @BeforeId";
            var parameters = new Dictionary<string, object> { { "BeforeId", beforeId } };

            AppendRecipientFilter(ref whereClause, parameters, recipientUris);

            var filter = new LocalScopedDb.Filter
            {
                Where = whereClause,
                Parameters = parameters
            };

            var grants = _localDbService.GetBadgeRecords(filter);
            return grants.Count > 0;
        }

        /// <summary>
        /// Resolves an attributedTo value to a list of recipient URIs.
        /// Accepts a full URI (returned as-is) or a fediverse handle (@user@domain or user@domain)
        /// which triggers a WebFinger lookup to discover all associated URIs.
        /// </summary>
        private async Task<List<string>?> ResolveAttributedToUris(string? attributedTo)
        {
            if (string.IsNullOrEmpty(attributedTo))
                return null;

            // If it's already a URI, return it directly
            if (attributedTo.StartsWith("http://") || attributedTo.StartsWith("https://"))
                return new List<string> { attributedTo };

            // Parse as a fediverse handle: @user@domain or user@domain
            var handle = attributedTo.TrimStart('@');
            var parts = handle.Split('@');
            if (parts.Length != 2 || string.IsNullOrEmpty(parts[0]) || string.IsNullOrEmpty(parts[1]))
            {
                _logger.LogWarning("Invalid attributedTo handle format: {AttributedTo}", attributedTo);
                return new List<string> { attributedTo };
            }

            var user = parts[0];
            var domain = parts[1];
            var webFingerUrl = $"https://{domain}/.well-known/webfinger?resource=acct:{Uri.EscapeDataString(user)}@{Uri.EscapeDataString(domain)}";

            try
            {
                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Add("Accept", "application/jrd+json, application/json");
                var response = await client.GetAsync(webFingerUrl);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var uris = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // Collect aliases
                if (root.TryGetProperty("aliases", out var aliases) && aliases.ValueKind == JsonValueKind.Array)
                {
                    foreach (var alias in aliases.EnumerateArray())
                    {
                        var val = alias.GetString();
                        if (!string.IsNullOrEmpty(val) && val.StartsWith("https://"))
                            uris.Add(val);
                    }
                }

                // Collect href from links
                if (root.TryGetProperty("links", out var links) && links.ValueKind == JsonValueKind.Array)
                {
                    foreach (var link in links.EnumerateArray())
                    {
                        if (link.TryGetProperty("href", out var href))
                        {
                            var val = href.GetString();
                            if (!string.IsNullOrEmpty(val) && val.StartsWith("https://"))
                                uris.Add(val);
                        }
                    }
                }

                // Collect subject if it's a URI
                if (root.TryGetProperty("subject", out var subject))
                {
                    var val = subject.GetString();
                    if (!string.IsNullOrEmpty(val) && val.StartsWith("https://"))
                        uris.Add(val);
                }

                if (uris.Count > 0)
                {
                    _logger.LogInformation("WebFinger resolved {Handle} to {Count} URIs: {Uris}", attributedTo, uris.Count, string.Join(", ", uris));
                    return uris.ToList();
                }

                _logger.LogWarning("WebFinger for {Handle} returned no usable URIs", attributedTo);
                return new List<string> { attributedTo };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "WebFinger lookup failed for {Handle}, falling back to literal value", attributedTo);
                return new List<string> { attributedTo };
            }
        }

        /// <summary>
        /// Appends an IssuedToSubjectUri IN (...) filter clause when recipient URIs are provided.
        /// </summary>
        private static void AppendRecipientFilter(ref string whereClause, Dictionary<string, object> parameters, List<string>? recipientUris, string tablePrefix = "")
        {
            if (recipientUris == null || recipientUris.Count == 0)
                return;

            if (recipientUris.Count == 1)
            {
                whereClause += $" AND {tablePrefix}IssuedToSubjectUri = @RecipientUri0";
                parameters.Add("RecipientUri0", recipientUris[0]);
            }
            else
            {
                var paramNames = new List<string>();
                for (int i = 0; i < recipientUris.Count; i++)
                {
                    var paramName = $"RecipientUri{i}";
                    paramNames.Add($"@{paramName}");
                    parameters.Add(paramName, recipientUris[i]);
                }
                whereClause += $" AND {tablePrefix}IssuedToSubjectUri IN ({string.Join(", ", paramNames)})";
            }
        }
    }
}
