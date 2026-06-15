namespace BadgeFed.Services;

/// <summary>
/// Singleton service that allows waking up the JobExecutor immediately
/// instead of waiting for the next polling interval.
/// </summary>
public sealed class JobSignal
{
    private readonly SemaphoreSlim _semaphore = new(0);

    /// <summary>
    /// Signal the job executor to wake up and process pending work.
    /// Safe to call multiple times; extra signals are harmless.
    /// </summary>
    public void Signal()
    {
        // Release only if no one is already waiting with a queued signal
        if (_semaphore.CurrentCount == 0)
            _semaphore.Release();
    }

    /// <summary>
    /// Wait for a signal or until the timeout elapses (fallback polling).
    /// Returns true if signaled, false if timed out.
    /// </summary>
    public Task<bool> WaitAsync(int timeoutMs, CancellationToken cancellationToken)
        => _semaphore.WaitAsync(timeoutMs, cancellationToken);
}
