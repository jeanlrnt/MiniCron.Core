namespace MiniCron.Core.Models;

public record CronJob(string CronExpression, Func<IServiceProvider, CancellationToken, Task> Action, TimeSpan? Timeout = null)
{
    /// <summary>
    /// Unique identifier for this job instance to ensure proper dictionary key uniqueness.
    /// This prevents issues where multiple jobs with the same cron expression and delegate
    /// would otherwise be treated as equal due to record value-based equality.
    /// </summary>
    public Guid Id { get; } = Guid.NewGuid();

}