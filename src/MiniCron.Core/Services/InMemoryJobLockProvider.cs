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
    /// Attempts to acquire a lock for the specified job ID.
    /// </summary>
    /// <param name="jobId">The unique identifier of the job.</param>
    /// <param name="ttl">The time-to-live for the lock.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>True if the lock was acquired; otherwise, false.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if this provider has been disposed.</exception>
    public async Task<bool> TryAcquireAsync(Guid jobId, TimeSpan ttl, CancellationToken cancellationToken)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(InMemoryJobLockProvider));
        }

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

            // Small backoff to avoid tight-looping
            try
            {
                await Task.Delay(5, cancellationToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
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
