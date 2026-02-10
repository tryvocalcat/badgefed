using Microsoft.Extensions.Logging;
using ActivityPubDotNet.Core;
using System.Text.Json;
using BadgeFed.Core;

namespace BadgeFed.Services;

public class JobProcessor
{
    private readonly string[] _domains;
    private readonly int _maxJobsPerRun;
   
    public ILogger? Logger = null;

    public JobProcessor(IConfiguration configuration, ILogger<JobProcessor>? logger = null)
    {
        _domains = configuration.GetSection("BadgesDomains").Get<string[]>() ?? new[] { "example.com" };
        _maxJobsPerRun = Environment.GetEnvironmentVariable("MAX_JOBS_PER_RUN") != null 
            ? int.Parse(Environment.GetEnvironmentVariable("MAX_JOBS_PER_RUN")!) 
            : 5; // Default to 5 jobs per run
        Logger = logger;
    }

    public async Task<int> DoWorkAsync(CancellationToken stoppingToken)
    {
        var jobsProcessed = 0;

        foreach (var domainRaw in _domains)
        {
            // removing port
            var domain = domainRaw.Split(':')[0];

            Logger?.LogInformation($"Processing jobs for domain: {domain}");
            
            // domain could be localhost:5000 we need to take just the hostname portion of it
            var db = new LocalScopedDb(domain);

            var openBadgeService = new OpenBadgeService(db);
            var badgeService = new BadgeService(db, openBadgeService);
            var badgeProcessor = new BadgeProcessor(db, badgeService);
            
            // 3 actions: notify of grant, process grant, broadcast grant
            try
            {
                var queued = await QueuePendingGrantsAsync(db, domain);
                if (queued > 0)
                {
                    Logger?.LogInformation("Queued {Count} grants for domain {Domain}", queued, domain);
                }
            } catch (Exception ex)
            {
                Logger?.LogError(ex, "Error queuing grants for domain {Domain}", domain);
            }

            try
            {
                await ProcessNextNotifyGrantAsync(db, badgeProcessor);
            } catch (Exception ex)
            {
                Logger?.LogError(ex, "Error processing next notify grant for domain {Domain}", domain);
            }
            
            try {
                await ProcessFollowersAsync(db, badgeProcessor);
            } catch (Exception ex)
            {
                Logger?.LogError(ex, "Error processing followers for domain {Domain}", domain);
            }

            try
            {
                // Process next queue work
                jobsProcessed += await ProcessNextQueueWorkAsync(domain);
            } catch (Exception ex)
            {
                Logger?.LogError(ex, "Error processing queue work for domain {Domain}", domain);
            }
        }
        
        return jobsProcessed;
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

    private async Task<int> QueuePendingGrantsAsync(LocalScopedDb dbService, string domain)
    {
        Logger?.LogInformation("Checking for pending grants to queue.");

        var pendingRecords = dbService.GetBadgeRecordsToProcess();

        if (pendingRecords.Count == 0)
        {
            return 0;
        }

        Logger?.LogInformation("Found {Count} pending grants to queue for domain {Domain}.", pendingRecords.Count, domain);

        var jobQueue = new JobQueueService(domain, Logger as ILogger<JobQueueService>);
        var queued = 0;

        foreach (var record in pendingRecords)
        {
            try
            {
                // Skip if any job already exists for this grant (pending, processing, completed, or failed)
                if (jobQueue.HasExistingJob("process_grant", "BadgeRecord", record.Id.ToString()))
                {
                    Logger?.LogInformation("Grant ID {GrantId} already has a job in the queue, skipping.", record.Id);
                    continue;
                }

                var payload = new { GrantId = record.Id };
                await jobQueue.AddJobAsync("process_grant", payload, createdBy: "JobProcessor", notes: $"Auto-queued grant {record.Id}", entityType: "BadgeRecord", entityId: record.Id.ToString());
                queued++;
                Logger?.LogInformation("Queued grant ID {GrantId} for processing.", record.Id);
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Failed to queue grant ID {GrantId}", record.Id);
            }
        }

        return queued;
    }

    private async Task ProcessFollowersAsync(LocalScopedDb _dbService, BadgeProcessor _badgeProcessor)
    {
        Logger?.LogInformation("Checking for followers to process.");
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
    
    private async Task<int> ProcessNextQueueWorkAsync(string domain)
    {
        var jobQueue = new JobQueueService(domain, Logger as ILogger<JobQueueService>);
        
        Logger?.LogInformation("Processing up to {MaxJobs} queue jobs for domain {Domain}", _maxJobsPerRun, domain);

        var count = 0;
        
        // Process multiple jobs in one run
        for (int i = 0; i < _maxJobsPerRun; i++)
        {
            // Peek at the highest priority / oldest message
            var job = await jobQueue.GetNextJobAsync();
            
            if (job == null)
            {
                // No more jobs in queue
                Logger?.LogInformation("No more jobs in queue for domain {Domain} after processing {ProcessedCount} jobs", domain, i);
                break;
            }
            
            Logger?.LogInformation("Found queue job {JobId} of type {JobType} for processing ({JobNumber}/{MaxJobs})", 
                job.Id, job.JobType, i + 1, _maxJobsPerRun);
            
            count++;
            
            try
            {
                // Process different job types
                switch (job.JobType)
                {
                    case "accept_follow_request":
                        await ProcessAcceptFollowRequestJob(job, domain);
                        break;
                    case "unfollow":
                        await ProcessUnfollowJob(job, domain);
                        break;
                    case "process_quote_request":
                        await ProcessQuoteRequestJob(job, domain);
                        break;
                    case "create_activity":
                        await ProcessCreateActivityJob(job, domain);
                        break;
                    case "process_announce":
                        await ProcessAnnounceJob(job, domain);
                        break;
                    case "process_grant":
                        await ProcessGrantJob(job, domain);
                        break;
                    default:
                        Logger?.LogWarning("Unknown job type {JobType} for job {JobId}", job.JobType, job.Id);
                        await jobQueue.AddJobLogAsync(job.Id, $"Unknown job type: {job.JobType}");
                        await jobQueue.FailJobAsync(job.Id, $"Unknown job type: {job.JobType}", false);
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Failed to process job {JobId} of type {JobType}", job.Id, job.JobType);
                await jobQueue.FailJobAsync(job.Id, ex.Message);
            }
            
            Logger?.LogInformation("Completed placeholder processing for job {JobId} ({JobNumber}/{MaxJobs})", job.Id, i + 1, _maxJobsPerRun);
        }
    
        return count;
    }

    private async Task ProcessGrantJob(SimpleJob job, string domain)
    {
        var jobQueue = new JobQueueService(domain, Logger as ILogger<JobQueueService>);

        try
        {
            if (string.IsNullOrEmpty(job.Payload))
            {
                throw new InvalidOperationException("Job payload is empty");
            }

            var payload = JsonDocument.Parse(job.Payload);
            var grantId = payload.RootElement.GetProperty("GrantId").GetInt64();

            await jobQueue.AddJobLogAsync(job.Id, $"Processing grant ID: {grantId}");

            var db = new LocalScopedDb(domain);
            var openBadgeService = new OpenBadgeService(db);
            var badgeService = new BadgeService(db, openBadgeService);
            var badgeProcessor = new BadgeProcessor(db, badgeService);

            var record = await badgeProcessor.SignAndGenerateBadge(grantId);

            if (record != null)
            {
                await jobQueue.AddJobLogAsync(job.Id, $"Signed and generated badge for grant {grantId}, broadcasting.");
                await badgeProcessor.BroadcastGrant(grantId);
                await badgeProcessor.NotifyProcessedGrant(grantId);
                await jobQueue.AddJobLogAsync(job.Id, $"Successfully processed grant {grantId}");
            }
            else
            {
                await jobQueue.AddJobLogAsync(job.Id, $"SignAndGenerateBadge returned null for grant {grantId}");
            }

            await jobQueue.CompleteJobAsync(job.Id);
            Logger?.LogInformation("Successfully processed grant job {JobId} for grant {GrantId}", job.Id, grantId);
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Failed to process grant job {JobId}", job.Id);
            await jobQueue.AddJobLogAsync(job.Id, $"FAILED: {ex.Message}");
            await jobQueue.FailJobAsync(job.Id, ex.Message);
        }
    }

    private async Task ProcessAcceptFollowRequestJob(SimpleJob job, string domain)
    {
        Logger?.LogInformation("Starting processing of accept follow request job {JobId}", job.Id);
        var jobQueue = new JobQueueService(domain, Logger as ILogger<JobQueueService>);
        
        try
        {
            // Deserialize the InboxMessage from the job payload
            if (string.IsNullOrEmpty(job.Payload))
            {
                throw new InvalidOperationException("Job payload is empty");
            }
            
            var message = JsonSerializer.Deserialize<InboxMessage>(job.Payload);
            if (message == null)
            {
                throw new InvalidOperationException("Failed to deserialize InboxMessage from job payload");
            }
            
            await jobQueue.AddJobLogAsync(job.Id, $"Processing accept follow request from actor: {message.Actor}");
            
            // Create database and services
            var db = new LocalScopedDb(domain);
            var followService = new FollowService();
            followService.Logger = Logger;
            
            // Process the follow request
            await followService.AcceptFollowRequest(message, db);
            
            await jobQueue.AddJobLogAsync(job.Id, $"Successfully processed accept follow request from actor: {message.Actor}");
            await jobQueue.CompleteJobAsync(job.Id);
            
            Logger?.LogInformation("Successfully processed accept follow request job {JobId} for actor {Actor}", job.Id, message.Actor);
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Failed to process accept follow request job {JobId}", job.Id);
            await jobQueue.AddJobLogAsync(job.Id, $"FAILED: {ex.Message}");
            await jobQueue.FailJobAsync(job.Id, ex.Message);
        }
    }

    private async Task ProcessUnfollowJob(SimpleJob job, string domain)
    {
        var jobQueue = new JobQueueService(domain, Logger as ILogger<JobQueueService>);
        
        try
        {
            // Deserialize the InboxMessage from the job payload
            if (string.IsNullOrEmpty(job.Payload))
            {
                throw new InvalidOperationException("Job payload is empty");
            }
            
            var message = JsonSerializer.Deserialize<InboxMessage>(job.Payload);
            if (message == null)
            {
                throw new InvalidOperationException("Failed to deserialize InboxMessage from job payload");
            }
            
            await jobQueue.AddJobLogAsync(job.Id, $"Processing unfollow request from actor: {message.Actor}");
            
            // Create follow service
            var followService = new FollowService();
            followService.Logger = Logger;
            
            // Process the unfollow request
            await followService.Unfollow(message);
            
            await jobQueue.AddJobLogAsync(job.Id, $"Successfully processed unfollow request from actor: {message.Actor}");
            await jobQueue.CompleteJobAsync(job.Id);
            
            Logger?.LogInformation("Successfully processed unfollow job {JobId} for actor {Actor}", job.Id, message.Actor);
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Failed to process unfollow job {JobId}", job.Id);
            await jobQueue.AddJobLogAsync(job.Id, $"FAILED: {ex.Message}");
            await jobQueue.FailJobAsync(job.Id, ex.Message);
        }
    }

    private async Task ProcessQuoteRequestJob(SimpleJob job, string domain)
    {
        var jobQueue = new JobQueueService(domain, Logger as ILogger<JobQueueService>);
        
        try
        {
            // Deserialize the InboxMessage from the job payload
            if (string.IsNullOrEmpty(job.Payload))
            {
                throw new InvalidOperationException("Job payload is empty");
            }
            
            var message = JsonSerializer.Deserialize<InboxMessage>(job.Payload);
            if (message == null)
            {
                throw new InvalidOperationException("Failed to deserialize InboxMessage from job payload");
            }
            
            await jobQueue.AddJobLogAsync(job.Id, $"Processing quote request from actor: {message.Actor}");
            
            // Create database and services
            var db = new LocalScopedDb(domain);
            var quoteRequestService = new QuoteRequestService(db);
            quoteRequestService.Logger = Logger;
            
            // Process the quote request
            await quoteRequestService.ProcessQuoteRequest(message);
            
            await jobQueue.AddJobLogAsync(job.Id, $"Successfully processed quote request from actor: {message.Actor}");
            await jobQueue.CompleteJobAsync(job.Id);
            
            Logger?.LogInformation("Successfully processed quote request job {JobId} for actor {Actor}", job.Id, message.Actor);
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Failed to process quote request job {JobId}", job.Id);
            await jobQueue.AddJobLogAsync(job.Id, $"FAILED: {ex.Message}");
            await jobQueue.FailJobAsync(job.Id, ex.Message);
        }
    }

    private async Task ProcessCreateActivityJob(SimpleJob job, string domain)
    {
        var jobQueue = new JobQueueService(domain, Logger as ILogger<JobQueueService>);
        
        try
        {
            // Deserialize the InboxMessage from the job payload
            if (string.IsNullOrEmpty(job.Payload))
            {
                throw new InvalidOperationException("Job payload is empty");
            }
            
            var message = JsonSerializer.Deserialize<InboxMessage>(job.Payload);
            if (message == null)
            {
                throw new InvalidOperationException("Failed to deserialize InboxMessage from job payload");
            }
            
            await jobQueue.AddJobLogAsync(job.Id, $"Processing create activity from actor: {message.Actor}");
            
            // Create database and services
            var db = new LocalScopedDb(domain);
            var repliesService = new RepliesService();
            var openBadgeService = new OpenBadgeService(db);
            var badgeService = new BadgeService(db, openBadgeService);
            var badgeProcessor = new BadgeProcessor(db, badgeService);
            var externalBadgeService = new ExternalBadgeService(badgeProcessor);
            var createNoteService = new CreateNoteService(repliesService, externalBadgeService);
            createNoteService.Logger = Logger;
            
            // Process the create activity
            var result = await createNoteService.ProcessMessage(message, db);
            
            await jobQueue.AddJobLogAsync(job.Id, $"Create activity result: {result.Type}");
            
            // Handle the result based on type
            switch (result.Type)
            {
                case CreateNoteResultType.ExternalBadgeProcessed:
                    if (result.BadgeRecord != null)
                    {
                        await jobQueue.AddJobLogAsync(job.Id, $"External badge processed, announcing grant for badge {result.BadgeRecord.Id}");
                        await badgeProcessor.AnnounceGrantByMainActor(result.BadgeRecord);
                    }
                    else
                    {
                        await jobQueue.AddJobLogAsync(job.Id, "ExternalBadgeProcessed result missing BadgeRecord");
                    }
                    break;
                case CreateNoteResultType.Error:
                    await jobQueue.AddJobLogAsync(job.Id, $"Error processing create activity: {result.ErrorMessage}");
                    if (result.Exception != null)
                    {
                        Logger?.LogError(result.Exception, "Create activity exception details");
                    }
                    break;
                case CreateNoteResultType.Reply:
                    await jobQueue.AddJobLogAsync(job.Id, "Processed as reply to existing badge");
                    break;
                case CreateNoteResultType.NotProcessed:
                    await jobQueue.AddJobLogAsync(job.Id, "Create activity not processed");
                    break;
                default:
                    await jobQueue.AddJobLogAsync(job.Id, $"Unknown result type: {result.Type}");
                    break;
            }
            
            await jobQueue.AddJobLogAsync(job.Id, $"Successfully processed create activity from actor: {message.Actor}");
            await jobQueue.CompleteJobAsync(job.Id);
            
            Logger?.LogInformation("Successfully processed create activity job {JobId} for actor {Actor}", job.Id, message.Actor);
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Failed to process create activity job {JobId}", job.Id);
            await jobQueue.AddJobLogAsync(job.Id, $"FAILED: {ex.Message}");
            await jobQueue.FailJobAsync(job.Id, ex.Message);
        }
    }

    private async Task ProcessAnnounceJob(SimpleJob job, string domain)
    {
        var jobQueue = new JobQueueService(domain, Logger as ILogger<JobQueueService>);
        
        try
        {
            // Deserialize the InboxMessage from the job payload
            if (string.IsNullOrEmpty(job.Payload))
            {
                throw new InvalidOperationException("Job payload is empty");
            }
            
            var message = JsonSerializer.Deserialize<InboxMessage>(job.Payload);
            if (message == null)
            {
                throw new InvalidOperationException("Failed to deserialize InboxMessage from job payload");
            }
            
            await jobQueue.AddJobLogAsync(job.Id, $"Processing announce from actor: {message.Actor}");
            
            // Create database and services
            var db = new LocalScopedDb(domain);
            var mainActor = db.GetMainActor();
            var repliesService = new RepliesService();
            var openBadgeService = new OpenBadgeService(db);
            var badgeService = new BadgeService(db, openBadgeService);
            var badgeProcessor = new BadgeProcessor(db, badgeService);
            var externalBadgeService = new ExternalBadgeService(badgeProcessor);
            var createNoteService = new CreateNoteService(repliesService, externalBadgeService);
            createNoteService.Logger = Logger;
            
            // Process the announce
            var result = await createNoteService.ProcessAnnounce(message, mainActor, db);
            
            await jobQueue.AddJobLogAsync(job.Id, $"Announce result: {result.Type}");
            
            // Handle the result based on type
            switch (result.Type)
            {
                case CreateNoteResultType.ExternalBadgeProcessed:
                    if (result.BadgeRecord != null)
                    {
                        await jobQueue.AddJobLogAsync(job.Id, $"External badge processed via announce, announcing grant for badge {result.BadgeRecord.Id}");
                        await badgeProcessor.AnnounceGrantByMainActor(result.BadgeRecord);
                    }
                    else
                    {
                        await jobQueue.AddJobLogAsync(job.Id, "ExternalBadgeProcessed result missing BadgeRecord");
                    }
                    break;
                case CreateNoteResultType.Error:
                    await jobQueue.AddJobLogAsync(job.Id, $"Error processing announce activity: {result.ErrorMessage}");
                    if (result.Exception != null)
                    {
                        Logger?.LogError(result.Exception, "Announce activity exception details");
                    }
                    break;
                case CreateNoteResultType.Reply:
                    await jobQueue.AddJobLogAsync(job.Id, "Processed as reply to existing badge");
                    break;
                case CreateNoteResultType.NotProcessed:
                    await jobQueue.AddJobLogAsync(job.Id, "Announce activity not processed");
                    break;
                default:
                    await jobQueue.AddJobLogAsync(job.Id, $"Unknown result type: {result.Type}");
                    break;
            }
            
            await jobQueue.AddJobLogAsync(job.Id, $"Successfully processed announce from actor: {message.Actor}");
            await jobQueue.CompleteJobAsync(job.Id);
            
            Logger?.LogInformation("Successfully processed announce job {JobId} for actor {Actor}", job.Id, message.Actor);
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Failed to process announce job {JobId}", job.Id);
            await jobQueue.AddJobLogAsync(job.Id, $"FAILED: {ex.Message}");
            await jobQueue.FailJobAsync(job.Id, ex.Message);
        }
    }
}