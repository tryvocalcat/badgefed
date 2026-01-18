using BadgeFed.Services;

public sealed class JobExecutor(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<JobExecutor> logger) : BackgroundService
{
    private const string ClassName = nameof(JobExecutor);

    // if we found something to process we wait a short time before checking again
    private const int delayWithJobs = 5_000;

    // if we found nothing to process we wait a longer time before checking again
    private const int delayNoJobs = 180_000;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "{Name} is running every {delayWithJobs} / {delayNoJobs}.", ClassName, delayWithJobs, delayNoJobs);

        await Task.Delay(delayNoJobs, stoppingToken);

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
            
            await Task.Delay(jobsProcessed > 0 ? delayWithJobs : delayNoJobs, stoppingToken);
        }
    }

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "{Name} is stopping.", ClassName);

        await base.StopAsync(stoppingToken);
    }
}