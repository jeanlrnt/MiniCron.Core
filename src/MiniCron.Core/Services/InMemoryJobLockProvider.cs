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

    /// <summary>
    /// Attempts to acquire a lock for the specified job with a time-to-live (TTL).
    /// Returns immediately with the result - does not wait or block if the lock is held.
    /// </summary>
    /// <param name="jobId">The unique identifier of the job to lock.</param>
    /// <param name="ttl">The time-to-live for the lock.</param>
    /// <param name="cancellationToken">Token to cancel the acquisition attempt (unused in this implementation).</param>
    /// <returns>
    /// True if the lock was successfully acquired; false if the lock is currently held by another execution.
    /// </returns>
    public Task<bool> TryAcquireAsync(Guid jobId, TimeSpan ttl, CancellationToken cancellationToken)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(InMemoryJobLockProvider));
        }

        var now = DateTimeOffset.UtcNow;
        var expiry = now.Add(ttl);

        // Try to add new lock
        if (_locks.TryAdd(jobId, expiry))
        {
            return Task.FromResult(true);
        }

        // If existing lock expired, try to replace it
        if (_locks.TryGetValue(jobId, out var existingExpiry))
        {
            if (existingExpiry <= now)
            {
                // Lock has expired, try to update it
                var refreshedExpiry = DateTimeOffset.UtcNow.Add(ttl);
                if (_locks.TryUpdate(jobId, refreshedExpiry, existingExpiry))
                {
                    return Task.FromResult(true);
                }
            }
        }

        // Lock is held and valid - return false immediately
        return Task.FromResult(false);
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
    }
}
