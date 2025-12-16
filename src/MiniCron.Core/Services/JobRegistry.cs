using MiniCron.Core.Models;

namespace MiniCron.Core.Services;

public class JobRegistry
{
    private readonly List<CronJob> _jobs = new();

    public void AddJob(string cronExpression, Func<IServiceProvider, CancellationToken, Task> action)
    {
        _jobs.Add(new CronJob(cronExpression, action));
    }

    public IReadOnlyList<CronJob> GetJobs() => _jobs;
}