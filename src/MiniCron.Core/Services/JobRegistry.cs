using MiniCron.Core.Helpers;
using MiniCron.Core.Models;

namespace MiniCron.Core.Services;

public class JobRegistry : IDisposable
{
    private readonly Dictionary<Guid, CronJob> _jobs = new();
    private readonly ReaderWriterLockSlim _lock = new();

    /// <summary>
    /// Schedules a new job with the specified cron expression and action.
    /// </summary>
    /// <param name="cronExpression">The cron expression defining when the job should run.</param>
    /// <param name="action">The action to execute when the job is triggered.</param>
    /// <returns>A unique identifier for the scheduled job.</returns>
    public Guid ScheduleJob(string cronExpression, Func<IServiceProvider, CancellationToken, Task> action)
    {
        // Validate the cron expression before adding the job
        CronHelper.ValidateCronExpression(cronExpression);

        var job = new CronJob(cronExpression, action);
        
        _lock.EnterWriteLock();
        try
        {
            _jobs.Add(job.Id, job);
            return job.Id;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Removes a scheduled job by its unique identifier.
    /// </summary>
    /// <param name="jobId">The unique identifier of the job to remove.</param>
    /// <returns>True if the job was found and removed; otherwise, false.</returns>
    public bool RemoveJob(Guid jobId)
    {
        _lock.EnterWriteLock();
        try
        {
            return _jobs.Remove(jobId);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Updates the schedule of an existing job.
    /// </summary>
    /// <param name="jobId">The unique identifier of the job to update.</param>
    /// <param name="newCronExpression">The new cron expression for the job.</param>
    /// <returns>True if the job was found and updated; otherwise, false.</returns>
    public bool UpdateSchedule(Guid jobId, string newCronExpression)
    {
        // Validate the new cron expression before updating
        CronHelper.ValidateCronExpression(newCronExpression);

        _lock.EnterWriteLock();
        try
        {
            if (_jobs.TryGetValue(jobId, out var existingJob))
            {
                // Create a new CronJob with the updated expression (records are immutable)
                var updatedJob = existingJob with { CronExpression = newCronExpression };
                _jobs[jobId] = updatedJob;
                return true;
            }
            return false;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Adds a new job with the specified cron expression and action.
    /// This method maintains backward compatibility with the original API.
    /// </summary>
    /// <param name="cronExpression">The cron expression defining when the job should run.</param>
    /// <param name="action">The action to execute when the job is triggered.</param>
    public void AddJob(string cronExpression, Func<IServiceProvider, CancellationToken, Task> action)
    {
        // Call ScheduleJob but discard the ID to match existing signature
        ScheduleJob(cronExpression, action);
    }

    public IReadOnlyList<CronJob> GetJobs()
    {
        _lock.EnterReadLock();
        try
        {
            return _jobs.Values.ToList();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public void Dispose()
    {
        _lock.Dispose();
    }
}