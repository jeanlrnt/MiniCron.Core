using MiniCron.Core.Helpers;
using MiniCron.Core.Models;
using Microsoft.Extensions.Logging;

namespace MiniCron.Core.Services;

public class JobRegistry : IDisposable
{
    private readonly Dictionary<Guid, CronJob> _jobs = new();
    private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.SupportsRecursion);
    private readonly ILogger<JobRegistry>? _logger;

    /// <summary>
    /// Occurs when a job is added to the registry.
    /// </summary>
    /// <remarks>
    /// This event is raised after the write lock is released, ensuring event handlers do not block registry operations.
    /// Event handlers observe the job state at the time of addition.
    /// <para>
    /// <strong>Note:</strong> Event handlers can safely call back into the JobRegistry (e.g., RemoveJob, UpdateSchedule, ScheduleJob)
    /// without risk of deadlock, as the event is invoked outside the lock.
    /// </para>
    /// </remarks>
    public event EventHandler<JobEventArgs>? JobAdded;
    
    /// <summary>
    /// Occurs when a job is removed from the registry.
    /// </summary>
    /// <remarks>
    /// This event is raised after the write lock is released, ensuring event handlers do not block registry operations.
    /// Event handlers observe the job state at the time of removal.
    /// <para>
    /// <strong>Note:</strong> Event handlers can safely call back into the JobRegistry (e.g., RemoveJob, UpdateSchedule, ScheduleJob)
    /// without risk of deadlock, as the event is invoked outside the lock.
    /// </para>
    /// </remarks>
    public event EventHandler<JobEventArgs>? JobRemoved;
    
    /// <summary>
    /// Occurs when a job's schedule is updated in the registry.
    /// </summary>
    /// <remarks>
    /// This event is raised after the write lock is released, ensuring event handlers do not block registry operations.
    /// Event handlers observe the job state at the time of update.
    /// <para>
    /// <strong>Note:</strong> Event handlers can safely call back into the JobRegistry (e.g., RemoveJob, UpdateSchedule, ScheduleJob)
    /// without risk of deadlock, as the event is invoked outside the lock.
    /// </para>
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
        JobEventArgs? eventArgs = null;

        _lock.EnterWriteLock();
        try
        {
            _jobs.Add(job.Id, job);
            _logger?.LogInformation("Job added: {JobId} {Cron}", job.Id, job.CronExpression);
            
            // Prepare event args if there are subscribers, but don't invoke yet
            if (JobAdded != null)
            {
                eventArgs = new JobEventArgs(job);
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }

        // Invoke event outside the lock
        if (eventArgs != null)
        {
            try
            {
                JobAdded?.Invoke(this, eventArgs);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Unhandled exception in JobAdded event handler for job {JobId}", job.Id);
            }
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
    /// Schedules a new job with the specified cron expression, action, and timeout.
    /// </summary>
    /// <param name="cronExpression">The cron expression defining when the job should run.</param>
    /// <param name="action">The action to execute when the job is triggered.</param>
    /// <param name="timeout">The maximum time allowed for the job to execute. If null, uses the default timeout.</param>
    /// <returns>A unique identifier for the scheduled job.</returns>
    public Guid ScheduleJob(string cronExpression, Func<IServiceProvider, CancellationToken, Task> action, TimeSpan? timeout)
    {
        CronHelper.ValidateCronExpression(cronExpression);

        var job = new CronJob(cronExpression, action, timeout);
        JobEventArgs? eventArgs = null;

        _lock.EnterWriteLock();
        try
        {
            _jobs.Add(job.Id, job);
            _logger?.LogInformation("Job added: {JobId} {Cron} Timeout: {Timeout}", job.Id, job.CronExpression, timeout);
            
            // Prepare event args if there are subscribers, but don't invoke yet
            if (JobAdded != null)
            {
                eventArgs = new JobEventArgs(job);
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }

        // Invoke event outside the lock
        if (eventArgs != null)
        {
            try
            {
                JobAdded?.Invoke(this, eventArgs);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Unhandled exception in JobAdded event handler for job {JobId}", job.Id);
            }
        }

        return job.Id;
    }

    /// <summary>
    /// Ergonomic overload accepting a token-aware delegate with timeout.
    /// </summary>
    public Guid ScheduleJob(string cronExpression, Func<CancellationToken, Task> action, TimeSpan? timeout)
    {
        return ScheduleJob(cronExpression, (_, ct) => action(ct), timeout);
    }

    /// <summary>
    /// Ergonomic overload accepting a simple synchronous action with timeout.
    /// </summary>
    public Guid ScheduleJob(string cronExpression, Action action, TimeSpan? timeout)
    {
        return ScheduleJob(cronExpression, (_, _) =>
        {
            action(); 
            return Task.CompletedTask;
        }, timeout);
    }

    /// <summary>
    /// Removes a scheduled job by its unique identifier.
    /// </summary>
    /// <param name="jobId">The unique identifier of the job to remove.</param>
    /// <returns>True if the job was found and removed; otherwise, false.</returns>
    public bool RemoveJob(Guid jobId)
    {
        CronJob? removedJob = null;
        JobEventArgs? eventArgs = null;

        _lock.EnterWriteLock();
        try
        {
            var removed = _jobs.Remove(jobId, out var job);
            if (!removed)
            {
                return false;
            }

            // At this point, job is guaranteed to be non-null because Remove returned true
            removedJob = job!;
            _logger?.LogInformation("Job removed: {JobId} {Cron}", jobId, removedJob.CronExpression);
            
            // Prepare event args if there are subscribers, but don't invoke yet
            if (JobRemoved != null)
            {
                eventArgs = new JobEventArgs(removedJob);
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }

        // Invoke event outside the lock
        if (eventArgs != null && removedJob != null)
        {
            try
            {
                JobRemoved?.Invoke(this, eventArgs);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Unhandled exception in JobRemoved event handler for job {JobId} {Cron}", jobId, removedJob.CronExpression);
            }
        }

        return removedJob != null;
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

        CronJob? updatedJob = null;
        CronJob? existingJob = null;
        JobEventArgs? eventArgs = null;

        _lock.EnterWriteLock();
        try
        {
            if (!_jobs.TryGetValue(jobId, out var oldJob))
            {
                return false;
            }
            
            existingJob = oldJob;
            updatedJob = existingJob with { CronExpression = newCronExpression };
            _jobs[jobId] = updatedJob;
            
            _logger?.LogInformation("Job updated: {JobId} {OldCron} -> {NewCron}", jobId, existingJob.CronExpression, newCronExpression);
            
            // Prepare event args if there are subscribers, but don't invoke yet
            if (JobUpdated != null)
            {
                eventArgs = new JobEventArgs(updatedJob, existingJob);
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }

        // Invoke event outside the lock
        if (eventArgs != null && updatedJob != null && existingJob != null)
        {
            try
            {
                JobUpdated?.Invoke(this, eventArgs);
            }
            catch (Exception ex)
            {
                _logger?.LogError(
                    ex,
                    "Unhandled exception in JobUpdated event handler for job {JobId} with cron change {OldCron} -> {NewCron}",
                    jobId,
                    existingJob.CronExpression,
                    updatedJob.CronExpression);
            }
        }
        
        return updatedJob != null;
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