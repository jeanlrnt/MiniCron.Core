using System.Collections.Concurrent;

namespace MiniCron.Core.Services;

/// <summary>
/// Simple in-memory job lock provider. Suitable for single-node scenarios or tests.
/// Not suitable for multi-process distributed locking.
/// </summary>
public class InMemoryJobLockProvider : IJobLockProvider, IDisposable
{
    private readonly ConcurrentDictionary<Guid, DateTimeOffset> _locks = new();
    private readonly SemaphoreSlim _lockReleasedSignal = new(0);

    /// <summary>
    /// Attempts to acquire a lock for the specified job with a time-to-live (TTL).
    /// Uses a combination of event-based signaling and progressive backoff for efficiency.
    /// </summary>
    /// <param name="jobId">The unique identifier of the job to lock.</param>
    /// <param name="ttl">The time-to-live for the lock.</param>
    /// <param name="cancellationToken">Token to cancel the acquisition attempt.</param>
    /// <returns>
    /// True if the lock was successfully acquired; false if the operation was cancelled
    /// or the lock could not be acquired within the cancellation period.
    /// When cancelled, the method returns false rather than throwing an exception.
    /// </returns>
    public async Task<bool> TryAcquireAsync(Guid jobId, TimeSpan ttl, CancellationToken cancellationToken)
    {
        var backoffDelay = 10; // Start with 10ms
        const int maxBackoffDelay = 500; // Cap at 500ms
        const double backoffMultiplier = 1.5; // Exponential growth factor

        while (!cancellationToken.IsCancellationRequested)
        {
            var now = DateTimeOffset.UtcNow;
            var expiry = now.Add(ttl);

            // Try add new lock
            if (_locks.TryAdd(jobId, expiry))
            {
                return true;
            }

            // If existing lock expired, try to replace it
            if (_locks.TryGetValue(jobId, out var existingExpiry))
            {
                var refreshedExpiry = DateTimeOffset.UtcNow.Add(ttl);
                if (existingExpiry <= DateTimeOffset.UtcNow
                    && _locks.TryUpdate(jobId, refreshedExpiry, existingExpiry))
                {
                    return true;
                }
            }

            // Wait for either a lock release signal or backoff delay
            // This combines event-based signaling with progressive backoff
            try
            {
                await _lockReleasedSignal.WaitAsync(backoffDelay, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Cancellation requested, return false as documented
                return false;
            }

            // Increase backoff delay exponentially, capped at maxBackoffDelay
            backoffDelay = Math.Min((int)(backoffDelay * backoffMultiplier), maxBackoffDelay);
        }

        return false;
    }

    public Task ReleaseAsync(Guid jobId)
    {
        _locks.TryRemove(jobId, out _);
        
        // Signal waiting threads that a lock has been released
        // Note: If CurrentCount is already at max, this is a no-op
        if (_lockReleasedSignal.CurrentCount == 0)
        {
            _lockReleasedSignal.Release();
        }
        
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _locks.Clear();
        _lockReleasedSignal.Dispose();
    }
}
