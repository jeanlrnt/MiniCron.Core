namespace MiniCron.Core.Models;

public record CronJob(string CronExpression, Func<IServiceProvider, CancellationToken, Task> Action);