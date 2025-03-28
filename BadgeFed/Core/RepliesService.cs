using Microsoft.Extensions.Logging;

namespace ActivityPubDotNet.Core
{
    public class RepliesService
    {
        public ILogger? Logger { get; set; }

        public Task AddReply(InboxMessage message)
        {
            Logger?.LogInformation("Adding a reply.");
            // TODO: Implement reply handling

            Console.WriteLine($"Adding a reply. {System.Text.Json.JsonSerializer.Serialize(message)}");
            return Task.CompletedTask;
        }
    }
}