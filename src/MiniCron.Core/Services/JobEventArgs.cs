using MiniCron.Core.Models;

namespace MiniCron.Core.Services;

public class JobEventArgs : EventArgs
{
    public CronJob Job { get; }
    public CronJob? PreviousJob { get; }

    public JobEventArgs(CronJob job, CronJob? previousJob = null)
    {
        Job = job;
        PreviousJob = previousJob;
    }
}
