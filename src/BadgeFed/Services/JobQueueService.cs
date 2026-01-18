using Microsoft.Extensions.Logging;
using System.Data.SQLite;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace BadgeFed.Services;

public class SimpleJob
{
    public long Id { get; set; }
    public string JobType { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public string? Payload { get; set; }
    public int MaxRetries { get; set; } = 3;
    public int CurrentRetry { get; set; } = 0;
    public string? LastError { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ScheduledFor { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? Notes { get; set; }
}

public class JobQueueService
{
    private readonly ILogger<JobQueueService>? _logger;
    private readonly IHttpContextAccessor? _httpContextAccessor;
    private readonly string? _fixedDomain;

    // Constructor for HTTP context-aware usage (scoped services)
    public JobQueueService(IHttpContextAccessor httpContextAccessor, ILogger<JobQueueService>? logger = null)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        _fixedDomain = null;
    }
    
    // Constructor for direct domain usage (background services)
    public JobQueueService(string domain, ILogger<JobQueueService>? logger = null)
    {
        _httpContextAccessor = null;
        _logger = logger;
        _fixedDomain = domain;
    }
    
    private string GetCurrentDomain()
    {
        string domain;
        
        if (_fixedDomain != null)
        {
            domain = _fixedDomain;
        }
        else
        {
            domain = _httpContextAccessor?.HttpContext?.Request.Host.Host ?? "localhost";
        }
        
        // Normalize domain the same way as LocalDbFactory
        return domain.Trim().ToLowerInvariant().TrimEnd('/').Replace(":", "_");
    }
    
    private QueueDb GetQueueDb()
    {
        var domain = GetCurrentDomain();
        return new QueueDb($"{domain}-queue.db");
    }
    
    // Public method for admin pages to access the QueueDb
    public QueueDb GetQueueDbInstance()
    {
        return GetQueueDb();
    }

    /// <summary>
    /// Add a job to the queue
    /// </summary>
    public async Task<long> AddJobAsync(string jobType, object? payload = null, DateTime? scheduledFor = null, string? createdBy = null, string? notes = null)
    {
        var domain = GetCurrentDomain();
        using var connection = GetQueueDb().GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO Jobs (JobType, Domain, Payload, ScheduledFor, CreatedBy, Notes)
            VALUES (@jobType, @domain, @payload, @scheduledFor, @createdBy, @notes);
            SELECT last_insert_rowid();";

        command.Parameters.AddWithValue("@jobType", jobType);
        command.Parameters.AddWithValue("@domain", domain);
        command.Parameters.AddWithValue("@payload", payload != null ? JsonSerializer.Serialize(payload) : DBNull.Value);
        command.Parameters.AddWithValue("@scheduledFor", scheduledFor ?? DateTime.UtcNow);
        command.Parameters.AddWithValue("@createdBy", createdBy ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@notes", notes ?? (object)DBNull.Value);

        var jobId = (long)command.ExecuteScalar();

        _logger?.LogInformation("Added job {JobId} of type {JobType} for domain {Domain}", jobId, jobType, domain);
        
        return jobId;
    }

    /// <summary>
    /// Get the next job to process
    /// </summary>
    public async Task<SimpleJob?> GetNextJobAsync()
    {
        var domain = GetCurrentDomain();
        using var connection = GetQueueDb().GetConnection();
        connection.Open();

        using var transaction = connection.BeginTransaction();
        try
        {
            // Get next pending job
            var selectCommand = connection.CreateCommand();
            selectCommand.Transaction = transaction;
            selectCommand.CommandText = @"
                SELECT Id, JobType, Domain, Status, Payload, MaxRetries, CurrentRetry, 
                       LastError, CreatedAt, ScheduledFor, StartedAt, CompletedAt, 
                       ProcessedAt, CreatedBy, Notes 
                FROM Jobs 
                WHERE Status = 'pending' AND Domain = @domain AND ScheduledFor <= @now
                ORDER BY ScheduledFor ASC 
                LIMIT 1";
            selectCommand.Parameters.AddWithValue("@domain", domain);
            selectCommand.Parameters.AddWithValue("@now", DateTime.UtcNow);

            using var reader = selectCommand.ExecuteReader();
            if (!reader.Read())
            {
                return null;
            }

            var job = new SimpleJob
            {
                Id = reader.GetInt64(0), // Id
                JobType = reader.GetString(1), // JobType
                Domain = reader.GetString(2), // Domain
                Status = reader.GetString(3), // Status
                Payload = reader.IsDBNull(4) ? null : reader.GetString(4), // Payload
                MaxRetries = reader.GetInt32(5), // MaxRetries
                CurrentRetry = reader.GetInt32(6), // CurrentRetry
                LastError = reader.IsDBNull(7) ? null : reader.GetString(7), // LastError
                CreatedAt = reader.GetDateTime(8), // CreatedAt
                ScheduledFor = reader.GetDateTime(9), // ScheduledFor
                StartedAt = reader.IsDBNull(10) ? null : reader.GetDateTime(10), // StartedAt
                CompletedAt = reader.IsDBNull(11) ? null : reader.GetDateTime(11), // CompletedAt
                ProcessedAt = reader.IsDBNull(12) ? null : reader.GetDateTime(12), // ProcessedAt
                CreatedBy = reader.IsDBNull(13) ? null : reader.GetString(13), // CreatedBy
                Notes = reader.IsDBNull(14) ? null : reader.GetString(14) // Notes
            };
            reader.Close();

            // Mark as processing
            var updateCommand = connection.CreateCommand();
            updateCommand.Transaction = transaction;
            updateCommand.CommandText = @"
                UPDATE Jobs 
                SET Status = 'processing', StartedAt = @startedAt 
                WHERE Id = @id AND Status = 'pending'";
            updateCommand.Parameters.AddWithValue("@id", job.Id);
            updateCommand.Parameters.AddWithValue("@startedAt", DateTime.UtcNow);

            var rowsAffected = updateCommand.ExecuteNonQuery();
            if (rowsAffected == 0)
            {
                transaction.Rollback();
                return null; // Job was taken by another worker
            }

            transaction.Commit();
            job.Status = "processing";
            job.StartedAt = DateTime.UtcNow;

            _logger?.LogInformation("Processing job {JobId} of type {JobType}", job.Id, job.JobType);
            return job;
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            _logger?.LogError(ex, "Error getting next job");
            throw;
        }
    }

    /// <summary>
    /// Mark job as completed
    /// </summary>
    public async Task CompleteJobAsync(long jobId)
    {
        await UpdateJobStatusAsync(jobId, "completed");
        _logger?.LogInformation("Completed job {JobId}", jobId);
    }

    /// <summary>
    /// Mark job as failed and handle retry logic
    /// </summary>
    public async Task FailJobAsync(long jobId, string? error = null, bool canRetry = true)
    {
        using var connection = GetQueueDb().GetConnection();
        connection.Open();

        using var transaction = connection.BeginTransaction();
        try
        {
            // Get current job info
            var selectCommand = connection.CreateCommand();
            selectCommand.Transaction = transaction;
            selectCommand.CommandText = "SELECT CurrentRetry, MaxRetries FROM Jobs WHERE Id = @id";
            selectCommand.Parameters.AddWithValue("@id", jobId);

            using var reader = selectCommand.ExecuteReader();
            if (!reader.Read())
            {
                throw new InvalidOperationException($"Job {jobId} not found");
            }

            var currentRetry = reader.GetInt32(0); // CurrentRetry
            var maxRetries = reader.GetInt32(1); // MaxRetries
            reader.Close();

            if (canRetry && currentRetry < maxRetries)
            {
                // Schedule for retry with exponential backoff
                var retryDelay = TimeSpan.FromMinutes(Math.Pow(2, currentRetry));
                var scheduledFor = DateTime.UtcNow.Add(retryDelay);

                var updateCommand = connection.CreateCommand();
                updateCommand.Transaction = transaction;
                updateCommand.CommandText = @"
                    UPDATE Jobs 
                    SET Status = 'pending', CurrentRetry = CurrentRetry + 1, LastError = @error, 
                        ScheduledFor = @scheduledFor, StartedAt = NULL
                    WHERE Id = @id";
                
                updateCommand.Parameters.AddWithValue("@id", jobId);
                updateCommand.Parameters.AddWithValue("@error", error ?? (object)DBNull.Value);
                updateCommand.Parameters.AddWithValue("@scheduledFor", scheduledFor);
                updateCommand.ExecuteNonQuery();

                await AddJobLogAsync(jobId, $"RETRY {currentRetry + 1}/{maxRetries}: {error}", connection, transaction);
                
                _logger?.LogWarning("Job {JobId} failed, scheduled for retry {Retry}/{MaxRetries} at {ScheduledFor}: {Error}", 
                    jobId, currentRetry + 1, maxRetries, scheduledFor, error);
            }
            else
            {
                // Mark as permanently failed
                var updateCommand = connection.CreateCommand();
                updateCommand.Transaction = transaction;
                updateCommand.CommandText = @"
                    UPDATE Jobs 
                    SET Status = 'failed', LastError = @error, CompletedAt = @completedAt, ProcessedAt = @processedAt
                    WHERE Id = @id";
                
                updateCommand.Parameters.AddWithValue("@id", jobId);
                updateCommand.Parameters.AddWithValue("@error", error ?? (object)DBNull.Value);
                updateCommand.Parameters.AddWithValue("@completedAt", DateTime.UtcNow);
                updateCommand.Parameters.AddWithValue("@processedAt", DateTime.UtcNow);
                updateCommand.ExecuteNonQuery();

                await AddJobLogAsync(jobId, $"FAILED PERMANENTLY after {currentRetry} retries: {error}", connection, transaction);
                
                _logger?.LogError("Job {JobId} permanently failed after {Retry} retries: {Error}", 
                    jobId, currentRetry, error);
            }

            transaction.Commit();
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            _logger?.LogError(ex, "Error failing job {JobId}", jobId);
            throw;
        }
    }

    /// <summary>
    /// Add a log entry for a job
    /// </summary>
    public async Task AddJobLogAsync(long jobId, string message, SQLiteConnection? connection = null, SQLiteTransaction? transaction = null)
    {
        var shouldDisposeConnection = connection == null;
        if (connection == null)
        {
            connection = GetQueueDb().GetConnection();
            connection.Open();
        }

        try
        {
            var command = connection.CreateCommand();
            if (transaction != null)
                command.Transaction = transaction;
                
            command.CommandText = @"
                INSERT INTO JobLogs (JobId, Message)
                VALUES (@jobId, @message)";

            command.Parameters.AddWithValue("@jobId", jobId);
            command.Parameters.AddWithValue("@message", message);

            command.ExecuteNonQuery();
        }
        finally
        {
            if (shouldDisposeConnection)
                connection.Dispose();
        }
    }

    /// <summary>
    /// Get job count by status
    /// </summary>
    public async Task<Dictionary<string, int>> GetJobCountsAsync()
    {
        var domain = GetCurrentDomain();
        using var connection = GetQueueDb().GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT Status, COUNT(*) as Count 
            FROM Jobs 
            WHERE Domain = @domain
            GROUP BY Status";
        command.Parameters.AddWithValue("@domain", domain);

        var counts = new Dictionary<string, int>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            counts[reader.GetString(0)] = reader.GetInt32(1); // Status, Count
        }

        return counts;
    }

    private async Task UpdateJobStatusAsync(long jobId, string status)
    {
        using var connection = GetQueueDb().GetConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE Jobs 
            SET Status = @status, CompletedAt = @completedAt, ProcessedAt = @processedAt
            WHERE Id = @id";

        command.Parameters.AddWithValue("@id", jobId);
        command.Parameters.AddWithValue("@status", status);
        var now = DateTime.UtcNow;
        command.Parameters.AddWithValue("@completedAt", status == "completed" || status == "failed" ? now : DBNull.Value);
        command.Parameters.AddWithValue("@processedAt", status == "completed" || status == "failed" ? now : DBNull.Value);

        command.ExecuteNonQuery();
    }
}