namespace MiniCron.Core.Services;

/// <summary>
/// Abstraction for job locking to support single-run semantics across instances.
/// Implementations may use Redis, database, or in-memory locks.
/// </summary>
public interface IJobLockProvider
{
    /// <summary>
    /// Try to acquire a lock for the specified job id with a TTL.
    /// Returns true if the lock was acquired.
    /// </summary>
    Task<bool> TryAcquireAsync(Guid jobId, TimeSpan ttl, CancellationToken cancellationToken);

    /// <summary>
    /// Release the lock for the specified job id.
    /// </summary>
    Task ReleaseAsync(Guid jobId);
}
