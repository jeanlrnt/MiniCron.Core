using MiniCron.Core.Helpers;
using MiniCron.Core.Models;

namespace MiniCron.Core.Services;

public class JobRegistry : IDisposable
{
    private readonly List<CronJob> _jobs = new();
    private readonly ReaderWriterLockSlim _lock = new();

    public void AddJob(string cronExpression, Func<IServiceProvider, CancellationToken, Task> action)
    {
        // Validate the cron expression before adding the job
        CronHelper.ValidateCronExpression(cronExpression);

        _lock.EnterWriteLock();
        try
        {
            _jobs.Add(new CronJob(cronExpression, action));
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public IReadOnlyList<CronJob> GetJobs()
    {
        _lock.EnterReadLock();
        try
        {
            return _jobs.ToList();
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