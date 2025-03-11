using Microsoft.Extensions.Logging;

namespace ActivityPubDotNet.Core
{
    public class ActorHelper
    {
        public ILogger? Logger { get; set; }

        public static Task<ActorInfo> FetchActorInformationAsync(string? actorUrl)
        {
            // TODO: Fetch and parse actor details
            return Task.FromResult(new ActorInfo { Id = actorUrl ?? "", Name = "Test", Url = actorUrl ?? "", Inbox = actorUrl ?? "" });
        }

        public Task SendSignedRequest(string content, Uri inbox)
        {
            Logger?.LogInformation($"Sending signed request to {inbox} with content:\n{content}");
            // TODO: Implement signature logic
            return Task.CompletedTask;
        }
    }

    public class ActorInfo
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Url { get; set; } = "";
        public string Inbox { get; set; } = "";
    }
}