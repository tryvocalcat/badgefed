using System.Text.Json;
using BadgeFed.Models;
using BadgeFed.Services;
using Microsoft.Extensions.Logging;

namespace ActivityPubDotNet.Core
{
    public class FollowService
    {
        public ILogger? Logger { get; set; }

        private readonly LocalDbService _localDbService;

        public FollowService(LocalDbService localDbService)
        {
            _localDbService = localDbService;
        }

        public async Task Follow(InboxMessage message)
        {
            Logger?.LogInformation($"Follow action for actor: {message.Actor}");
            
            await CreateFollower(message);
            await SendAcceptedFollowRequest(message);
        }

        public async Task CreateFollower(InboxMessage message)
        {
            Logger?.LogDebug($"Follow request from: {message.Actor}");
            
            var actor = _localDbService.GetActorByFilter($"Uri = \"{message.Object}\"")!;

            var follower = new Follower()
            {
                FollowerUri = message.Actor!,
                Domain = new Uri(message.Actor!).Host,
                Parent = new Actor()
                {
                    Id = actor.Id
                }
            };

            _localDbService.UpsertFollower(follower);
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

                    var actor = _localDbService.GetActorByFilter($"Uri = \"{target}\"")!;
                    var actorHelper = new ActorHelper(actor.PrivateKeyPem!, actor.KeyId, Logger);

                    await actorHelper.SendSignedRequest(document, new Uri(actor.Inbox));
            **/

            return Task.CompletedTask;
        }

        public async Task<AcceptRequest> SendAcceptedFollowRequest(InboxMessage message)
        {
            // Target is the account to be followed
            var target = message.Object!.ToString();

            var actor = _localDbService.GetActorByFilter($"Uri = \"{target}\"")!;

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