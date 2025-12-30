using System.Collections.Concurrent;

namespace MiniCron.Core.Services;

/// <summary>
/// Simple in-memory job lock provider. Suitable for single-node scenarios or tests.
/// Not suitable for multi-process distributed locking.
/// </summary>
public class InMemoryJobLockProvider : IJobLockProvider, IDisposable
{
    private readonly ConcurrentDictionary<Guid, DateTimeOffset> _locks = new();
    private volatile bool _disposed;

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

    public Task ReleaseAsync(Guid jobId)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(InMemoryJobLockProvider));
        }

        _locks.TryRemove(jobId, out _);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _disposed = true;
        _locks.Clear();
    }
}
