using System.Collections.Concurrent;

namespace MiniCron.Core.Services;

/// <summary>
/// Simple in-memory job lock provider. Suitable for single-node scenarios or tests.
/// Not suitable for multi-process distributed locking.
/// </summary>
public class InMemoryJobLockProvider : IJobLockProvider, IDisposable
{
    private readonly ConcurrentDictionary<Guid, DateTimeOffset> _locks = new();

    public Task<bool> TryAcquireAsync(Guid jobId, TimeSpan ttl, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var expiry = now.Add(ttl);

        while (!cancellationToken.IsCancellationRequested)
        {
            // Try add new lock
            if (_locks.TryAdd(jobId, expiry))
            {
                return Task.FromResult(true);
            }

            // If existing lock expired, try to replace it
            if (_locks.TryGetValue(jobId, out var existingExpiry) && existingExpiry <= now)
            {
                if (_locks.TryUpdate(jobId, expiry, existingExpiry))
                {
                    return Task.FromResult(true);
                }
            }

            // Small backoff to avoid tight-looping
            Thread.Sleep(5);
            now = DateTimeOffset.UtcNow;
        }

        return Task.FromResult(false);
    }

    public Task ReleaseAsync(Guid jobId)
    {
        _locks.TryRemove(jobId, out _);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _locks.Clear();
    }
}
