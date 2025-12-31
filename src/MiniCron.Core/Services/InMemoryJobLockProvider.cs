using System.Collections.Concurrent;

namespace MiniCron.Core.Services;

/// <summary>
/// Simple in-memory job lock provider. Suitable for single-node scenarios or tests.
/// Not suitable for multi-process distributed locking.
/// </summary>
/// <remarks>
/// Once <see cref="Dispose"/> is called, this provider should not be used for any operations.
/// Attempting to call <see cref="TryAcquireAsync"/> or <see cref="ReleaseAsync"/> after disposal
/// will throw <see cref="ObjectDisposedException"/>.
/// </remarks>
public class InMemoryJobLockProvider : IJobLockProvider, IDisposable
{
    private readonly ConcurrentDictionary<Guid, DateTimeOffset> _locks = new();
    private volatile bool _disposed;
    private readonly SemaphoreSlim _lockReleasedSignal = new(0, 1);

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
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(InMemoryJobLockProvider));
        }
        var backoffDelay = 10; // Start with 10ms
        const int maxBackoffDelay = 500; // Cap at 500ms

        while (!cancellationToken.IsCancellationRequested)
        {
            // Check disposed state before each operation to prevent race conditions
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(InMemoryJobLockProvider));
            }

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

            // Increase backoff delay with integer arithmetic (backoff * 3 / 2), capped at maxBackoffDelay
            backoffDelay = Math.Min(backoffDelay * 3 / 2, maxBackoffDelay);
        }

        return false;
    }

    /// <summary>
    /// Releases the lock for the specified job ID.
    /// </summary>
    /// <param name="jobId">The unique identifier of the job.</param>
    /// <returns>A completed task.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if this provider has been disposed.</exception>
    public Task ReleaseAsync(Guid jobId)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(InMemoryJobLockProvider));
        }

        _locks.TryRemove(jobId, out _);
        
        // Signal waiting threads that a lock has been released
        // Use try-catch to handle the case where count is already at max
        try
        {
            _lockReleasedSignal.Release();
        }
        catch (SemaphoreFullException)
        {
            // Already at max count, no action needed
        }
        
        return Task.CompletedTask;
    }

    /// <summary>
    /// Disposes the provider and clears all locks. This method is idempotent.
    /// After disposal, all operations on this provider will throw <see cref="ObjectDisposedException"/>.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _locks.Clear();
        _lockReleasedSignal.Dispose();
    }
}
