using BadgeFed.Services;

public sealed class JobExecutor(
    IServiceScopeFactory serviceScopeFactory,
    JobSignal jobSignal,
    ILogger<JobExecutor> logger) : BackgroundService
{
    private const string ClassName = nameof(JobExecutor);

    // if we found something to process we wait a short time before checking again
    private const int delayWithJobs = 30_000;

    // if we found nothing to process we wait a longer time before checking again
    private const int delayNoJobs = 120_000;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "{Name} is running every {delayWithJobs} / {delayNoJobs} (with signal wake-up).", ClassName, delayWithJobs, delayNoJobs);

        await jobSignal.WaitAsync(delayNoJobs, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            int jobsProcessed = 0;

            using IServiceScope scope = serviceScopeFactory.CreateScope();

            var scopedProcessingService =
                scope.ServiceProvider.GetRequiredService<JobProcessor>();

            try
            {
                jobsProcessed = await scopedProcessingService.DoWorkAsync(stoppingToken);
            } catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Error occurred executing {Name}.", ClassName);
            }

            var delay = jobsProcessed > 0 ? delayWithJobs : delayNoJobs;
            var signaled = await jobSignal.WaitAsync(delay, stoppingToken);

            if (signaled)
            {
                logger.LogInformation("{Name} woke up early via signal.", ClassName);
            }
        }
    }

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "{Name} is stopping.", ClassName);

        await base.StopAsync(stoppingToken);
    }
}