using BadgeFed.Services;

public sealed class JobExecutor(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<JobExecutor> logger) : BackgroundService
{
    private const string ClassName = nameof(JobExecutor);

    private const int delay = 60_000;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "{Name} is running every {delay}.", ClassName, delay);

        await Task.Delay(delay, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            using IServiceScope scope = serviceScopeFactory.CreateScope();

            var scopedProcessingService =
                scope.ServiceProvider.GetRequiredService<JobProcessor>();

            await scopedProcessingService.DoWorkAsync(stoppingToken);

            await Task.Delay(delay, stoppingToken);
        }
    }

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "{Name} is stopping.", ClassName);

        await base.StopAsync(stoppingToken);
    }
}