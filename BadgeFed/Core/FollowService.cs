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
            // TODO: Implement unfollow logic
            return Task.CompletedTask;
        }

        public async Task<AcceptRequest> SendAcceptedFollowRequest(InboxMessage message)
        {
            // Target is the account to be followed
            var target = message.Object!.ToString();

            // Actor is the account who wants to follow
            var actor = await ActorHelper.FetchActorInformationAsync(message.Actor);

            Logger?.LogInformation($"Actor: {actor.Id} - {actor.Name} - {actor.Url} => Target: {target}");

            //'#accepts/follows/'
            var acceptRequest = new AcceptRequest()
            {
                Context = "https://www.w3.org/ns/activitystreams",
                Id = $"{target}#accepts/follows/{actor.Id}",
                Actor = $"{target}",
                Object = new
                {
                    message.Id,
                    Actor = actor.Url,
                    Type = "Follow",
                    Object = $"{target}"
                }
            };

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };

            await _actorHelper.SendSignedRequest(JsonSerializer.Serialize(acceptRequest, options), new Uri(actor.Inbox));

            return acceptRequest;
        }
    }
}