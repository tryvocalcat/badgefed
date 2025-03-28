using Microsoft.Extensions.Logging;
using ActivityPubDotNet.Core;

namespace BadgeFed.Services;

public class JobProcessor
{
    private readonly LocalDbService _dbService;
    private readonly BadgeProcessor _badgeProcessor;

    public ILogger? Logger = null;

    public JobProcessor(LocalDbService dbService, BadgeProcessor badgeProcessor)
    {
        _dbService = dbService;
        _badgeProcessor = badgeProcessor;
    }

    public async Task DoWorkAsync(CancellationToken stoppingToken)
    {
        // 3 actions: notify of grant, process grant, broadcast grant
        await ProcessNextProcessGrantAsync();

        await ProcessNextNotifyGrantAsync();
    }

    private async Task ProcessNextNotifyGrantAsync()
    {
        var grantId = _dbService.PeekNotifyGrantId();

        if (grantId == null || grantId == 0)
        {
            return;
        }

        await _badgeProcessor.NotifyGrant(grantId);
    }

    private async Task ProcessNextProcessGrantAsync()
    {
        var grantId = _dbService.PeekProcessGrantId();

        if (grantId == null || grantId == 0)
        {
            return;
        }

        var record = await _badgeProcessor.SignAndGenerateBadge(grantId);

        if (record != null) {
            await _badgeProcessor.BroadcastGrant(grantId);
        }
    }   
}