using System.Text.Json;
using BadgeFed.Models;
using BadgeFed.Services;
using Microsoft.Extensions.Logging;

namespace ActivityPubDotNet.Core
{
    public class FollowService
    {
        public ILogger? Logger { get; set; }

        public FollowService()
        {
        }

        public async Task AcceptFollowRequest(InboxMessage message, LocalScopedDb db)
        {
            Logger?.LogInformation($"Follow action from actor: {message.Actor}");
            
            await CreateFollower(message, db);
            await SendAcceptedFollowRequest(message, db);
        }
        
        public async Task<ActivityPubActor?> FollowIssuer(Actor fromIssuer, string issuerToFollowUri)
        {
            try
            {
                Console.WriteLine($"Following {issuerToFollowUri} from {fromIssuer.Uri}");
                var actorHelper = new ActorHelper(fromIssuer.PrivateKeyPemClean!, fromIssuer.KeyId, Logger);

                // Fetch actor information
                var actor = await actorHelper.FetchActorInformationAsync(issuerToFollowUri);

                // Create follow activity
                var followId = $"{fromIssuer.Uri}/follow/{Guid.NewGuid()}";

                Console.WriteLine($"Follow ID: {followId}");
                var followActivity = new FollowActivity
                {
                    _context = "https://www.w3.org/ns/activitystreams",
                    Id = followId,
                    Type = "Follow",
                    Actor = fromIssuer.Uri?.ToString(),
                    _object = issuerToFollowUri
                };

                Console.WriteLine($"Follow activity: {JsonSerializer.Serialize(followActivity)}");
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                };

                Console.WriteLine($"Follow activity: {JsonSerializer.Serialize(followActivity, options)}");
                // Send follow request
                var followJson = JsonSerializer.Serialize(followActivity, options);
                Logger?.LogInformation($"Sending follow request: {followJson}");

                // Send follow request to the actor's inbox
                await actorHelper.SendPostSignedRequest(followJson, new Uri(actor.Inbox));

                Logger?.LogInformation($"Successfully sent follow request to {fromIssuer.Uri}");

                return actor;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to follow actor: {ex}");
                Logger?.LogError($"Failed to follow actor: {ex}");
                return null;
            }
        }

        public async Task CreateFollower(InboxMessage message, LocalScopedDb db)
        {
            Logger?.LogInformation($"Follow request from: {message.Actor} to {message.Object}");

            var actor = db.GetActorByFilter($"Uri = \"{message.Object}\"")!;

            if (actor == null)
            {
                // https://badges.vocalcat.com/view/actor/badges.vocalcat.com/admin to https://badges.vocalcat.com/actors/badges.vocalcat.com/admin
                var newUri = message.Object!.ToString().Replace("/view/actor/", "/actors/");
                
                actor = db.GetActorByFilter($"Uri = \"{newUri}\"")!;

                if (actor == null)
                {
                    Logger?.LogWarning($"Actor not found for URI: {newUri} or {message.Object}");
                    return;
                }
            }

            var follower = new Follower()
            {
                FollowerUri = message.Actor!,
                Domain = new Uri(message.Actor!).Host,
                Parent = new Actor()
                {
                    Id = actor.Id
                }
            };

            db.UpsertFollower(follower);
        }

        public Task Unfollow(InboxMessage message)
        {
            Logger?.LogInformation($"Unfollow action for actor: {message.Actor}");
            
            /**

            _logger.LogDebug($"Message received: {JsonSerializer.Serialize(message)}");

                    var follower = await ActorHelper.FetchActorInformationAsync(message.Actor);
                    _logger.LogInformation($"Actor: {follower.Id} - {follower.Name} - {follower.Url}");

                    var uuid = Guid.NewGuid();
                    var acceptRequest = new AcceptRequest
                    {
                        Context = "https://www.w3.org/ns/activitystreams",
                        Id = $"{_serverConfig.BaseDomain}/{uuid}",
                        Actor = $"{_serverConfig.BaseDomain}/{_serverConfig.ActorName}",
                        Object = JsonSerializer.Deserialize<dynamic>(JsonSerializer.Serialize(message, options), options)!
                    };

                    var document = JsonSerializer.Serialize(acceptRequest, options);
                    _logger.LogInformation($"Sending accept request to {follower.Inbox} - {document}");

                    var db = localDbFactory.GetInstance(new Uri(target));
                    var actor = db.GetActorByFilter($"Uri = \"{target}\"")!;
                    var actorHelper = new ActorHelper(actor.PrivateKeyPem!, actor.KeyId, Logger);

                    await actorHelper.SendSignedRequest(document, new Uri(actor.Inbox));
            **/

            return Task.CompletedTask;
        }

        public async Task<AcceptRequest> SendAcceptedFollowRequest(InboxMessage message, LocalScopedDb db)
        {
            // Target is the account to be followed
            var target = message.Object!.ToString();

            // target could be https://communitycredentials.org/view/actor/communitycredentials.org/issuer
            // but we need to check https://communitycredentials.org/actors/communitycredentials.org/issuer
            var targetClean = target!.Replace("/view/actor/", "/actors/");

            var actor = db.GetActorByFilter($"Uri = \"{targetClean}\"")!;

            var actorHelper = new ActorHelper(actor.PrivateKeyPemClean!, actor.KeyId, Logger);

            // Actor is the account who wants to follow
            var follower = await actorHelper.FetchActorInformationAsync(message.Actor);

            Logger?.LogInformation($"Actor: {follower.Id} - {follower.Name} - {follower.Url} => Target: {target}");

            //'#accepts/follows/'
            var acceptRequest = new AcceptRequest()
            {
                Context = "https://www.w3.org/ns/activitystreams",
                Id = $"{target}#accepts/follows/{follower.Id}",
                Actor = $"{target}",
                Object = new
                {
                    message.Id,
                    Actor = follower.Url,
                    Type = "Follow",
                    Object = $"{target}"
                }
            };

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };

            await actorHelper.SendPostSignedRequest(JsonSerializer.Serialize(acceptRequest, options), new Uri(follower.Inbox));

            return acceptRequest;
        }
    }
}