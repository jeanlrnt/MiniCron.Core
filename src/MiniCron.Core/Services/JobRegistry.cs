using MiniCron.Core.Helpers;
using MiniCron.Core.Models;
using Microsoft.Extensions.Logging;

namespace MiniCron.Core.Services;

public class JobRegistry : IDisposable
{
    private readonly Dictionary<Guid, CronJob> _jobs = new();
    private readonly ReaderWriterLockSlim _lock = new();
    private readonly ILogger<JobRegistry>? _logger;

    /// <summary>
    /// Occurs when a job is added to the registry.
    /// </summary>
    /// <remarks>
    /// This event is raised inside the write lock, ensuring event handlers observe consistent registry state.
    /// Event handlers should be lightweight to avoid blocking other registry operations.
    /// </remarks>
    public event EventHandler<JobEventArgs>? JobAdded;
    
    /// <summary>
    /// Occurs when a job is removed from the registry.
    /// </summary>
    /// <remarks>
    /// This event is raised inside the write lock, ensuring event handlers observe consistent registry state.
    /// Event handlers should be lightweight to avoid blocking other registry operations.
    /// </remarks>
    public event EventHandler<JobEventArgs>? JobRemoved;
    
    /// <summary>
    /// Occurs when a job's schedule is updated in the registry.
    /// </summary>
    /// <remarks>
    /// This event is raised inside the write lock, ensuring event handlers observe consistent registry state.
    /// Event handlers should be lightweight to avoid blocking other registry operations.
    /// </remarks>
    public event EventHandler<JobEventArgs>? JobUpdated;

    public JobRegistry(ILogger<JobRegistry>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Schedules a new job with the specified cron expression and action.
    /// </summary>
    /// <param name="cronExpression">The cron expression defining when the job should run.</param>
    /// <param name="action">The action to execute when the job is triggered.</param>
    /// <returns>A unique identifier for the scheduled job.</returns>
    public Guid ScheduleJob(string cronExpression, Func<IServiceProvider, CancellationToken, Task> action)
    {
        CronHelper.ValidateCronExpression(cronExpression);

        var job = new CronJob(cronExpression, action);

        _lock.EnterWriteLock();
        try
        {
            _jobs.Add(job.Id, job);
            _logger?.LogInformation("Job added: {JobId} {Cron}", job.Id, job.CronExpression);
            JobAdded?.Invoke(this, new JobEventArgs(job));
        }
        finally
        {
            _lock.ExitWriteLock();
        }
        return job.Id;
    }

    /// <summary>
    /// Ergonomic overload accepting a token-aware delegate.
    /// </summary>
    public Guid ScheduleJob(string cronExpression, Func<CancellationToken, Task> action)
    {
        return ScheduleJob(cronExpression, (_, ct) => action(ct));
    }

    /// <summary>
    /// Ergonomic overload accepting a simple synchronous action.
    /// </summary>
    public Guid ScheduleJob(string cronExpression, Action action)
    {
        return ScheduleJob(cronExpression, (_, _) =>
        {
            action(); 
            return Task.CompletedTask;
        });
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
            if (!_jobs.TryGetValue(jobId, out var job)) return false;
            
            var removed = _jobs.Remove(jobId);
            if (removed)
            {
                _logger?.LogInformation("Job removed: {JobId} {Cron}", jobId, job.CronExpression);
                JobRemoved?.Invoke(this, new JobEventArgs(job));
            }
            
            return removed;
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
        CronHelper.ValidateCronExpression(newCronExpression);

        _lock.EnterWriteLock();
        try
        {
            if (!_jobs.TryGetValue(jobId, out var existingJob))
            {
                return false;
            }
            
            var updatedJob = existingJob with { CronExpression = newCronExpression };
            _jobs[jobId] = updatedJob;
            
            _logger?.LogInformation("Job updated: {JobId} {OldCron} -> {NewCron}", jobId, existingJob.CronExpression, newCronExpression);
            JobUpdated?.Invoke(this, new JobEventArgs(updatedJob, existingJob));
            
            return true;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Adds a new job with the specified cron expression and action. Backward-compatible.
    /// </summary>
    /// <param name="cronExpression">The cron expression defining when the job should run.</param>
    /// <param name="action">The action to execute when the job is triggered.</param>
    public void AddJob(string cronExpression, Func<IServiceProvider, CancellationToken, Task> action)
    {
        ScheduleJob(cronExpression, action);
    }

    /// <summary>
    /// Adds a backward-compatible overload for simple actions.
    /// </summary>
    /// <param name="cronExpression">The cron expression defining when the job should run.</param>
    /// <param name="action">The action to execute when the job is triggered.</param>
    public void AddJob(string cronExpression, Action action)
    {
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