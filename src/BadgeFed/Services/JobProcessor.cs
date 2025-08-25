using Microsoft.Extensions.Logging;
using ActivityPubDotNet.Core;

namespace BadgeFed.Services;

public class JobProcessor
{
    private readonly string[] _domains;
   
    public ILogger? Logger = null;

    public JobProcessor(IConfiguration configuration)
    {
        _domains = configuration.GetSection("BadgesDomains").Get<string[]>() ?? new[] { "example.com" };
    }

    public async Task DoWorkAsync(CancellationToken stoppingToken)
    {
        foreach (var domain in _domains)
        {
            Console.WriteLine($"Processing jobs for domain: {domain}");

            // domain could be localhost:5000 we need to take just the hostname portion of it
            var db = new LocalScopedDb(domain);

            var openBadgeService = new OpenBadgeService(db);
            var badgeService = new BadgeService(db, openBadgeService);
            var badgeProcessor = new BadgeProcessor(db, badgeService);
            
            // 3 actions: notify of grant, process grant, broadcast grant
            await ProcessNextProcessGrantAsync(db, badgeProcessor);

            await ProcessNextNotifyGrantAsync(db, badgeProcessor);

            await ProcessFollowersAsync(db, badgeProcessor);
        }
    }

    private async Task ProcessNextNotifyGrantAsync(LocalScopedDb _dbService, BadgeProcessor _badgeProcessor)
    {
        var grantId = _dbService.PeekNotifyGrantId();

     //   Console.WriteLine($"Processing notify grant: {grantId}");

        if (grantId == 0)
        {
            return;
        }

        await _badgeProcessor.NotifyGrantAcceptLink(grantId);
    }

    private async Task ProcessNextProcessGrantAsync(LocalScopedDb _dbService, BadgeProcessor _badgeProcessor)
    {
      //  Console.WriteLine("Processing next grant...");

        var grantId = _dbService.PeekProcessGrantId();

        if (grantId == 0)
        {
           // Console.WriteLine("No grants to process.");
            return;
        }

        Console.WriteLine($"Processing grant ID: {grantId}");

        var record = await _badgeProcessor.SignAndGenerateBadge(grantId);

        if (record != null)
        {
            Console.WriteLine($"Processed grant: {record.Id}");
            await _badgeProcessor.BroadcastGrant(grantId);

            await _badgeProcessor.NotifyProcessedGrant(grantId);
        }
    }

    private async Task ProcessFollowersAsync(LocalScopedDb _dbService, BadgeProcessor _badgeProcessor)
    {
        var followers = _dbService.GetFollowersToProcess();

       // Console.WriteLine($"Processing followers: {followers.Count}");

        if (followers.Count == 0)
        {
            return;
        }

        foreach (var follower in followers)
        {
            try
            {
                await _badgeProcessor.ProcessFollowerAsync(follower);
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, $"Failed to process follower {follower.FollowerUri}");
            }
        }
    }
}