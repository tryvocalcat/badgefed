using Microsoft.Extensions.Logging;

namespace ActivityPubDotNet.Core
{
    public class FollowService
    {
        public ILogger? Logger { get; set; }

        public Task Follow(InboxMessage message)
        {
            Logger?.LogInformation($"Follow action for actor: {message.Actor}");
            // TODO: Implement follow logic
            return Task.CompletedTask;
        }

        public Task Unfollow(InboxMessage message)
        {
            Logger?.LogInformation($"Unfollow action for actor: {message.Actor}");
            // TODO: Implement unfollow logic
            return Task.CompletedTask;
        }
    }
}