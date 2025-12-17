using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MiniCron.Core.Helpers;
using System.Collections.Concurrent;

namespace MiniCron.Core.Services;

public class MiniCronBackgroundService : BackgroundService
{
    private readonly JobRegistry _registry;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MiniCronBackgroundService> _logger;
    private readonly ConcurrentDictionary<string, byte> _runningJobs = new();

    public MiniCronBackgroundService(
        JobRegistry registry,
        IServiceProvider serviceProvider,
        ILogger<MiniCronBackgroundService> logger)
    {
        _registry = registry;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait until the start of the next minute
        var now = DateTime.Now;
        var delayToNextMinute = 60 - now.Second;
        
        if (delayToNextMinute > 0)
        {
            await Task.Delay(TimeSpan.FromSeconds(delayToNextMinute), stoppingToken);
        }
    
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));

        // Initial run at startup
        await RunJobs(stoppingToken);

        // Subsequent runs every minute
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunJobs(stoppingToken);
        }
    }

    private async Task RunJobs(CancellationToken stoppingToken)
    {
        var now = DateTime.Now;

        foreach (var job in _registry.GetJobs())
        {
            try
            {
                if (CronHelper.IsDue(job.CronExpression, now))
                {
                    // Check if this job is already running to prevent duplicate executions
                    if (_runningJobs.TryAdd(job.CronExpression, 0))
                    {
                        // Run the task in "Fire and Forget" (Task.Run) to avoid blocking
                        // the scheduler if the task is long.
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await ExecuteJobScoped(job, stoppingToken);
                            }
                            finally
                            {
                                // Remove from running jobs when complete
                                _runningJobs.TryRemove(job.CronExpression, out _);
                            }
                        }, stoppingToken);
                    }
                    else
                    {
                        _logger.LogWarning("Job {Cron} is already running, skipping this execution", job.CronExpression);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error evaluating Cron expression: {Cron}", job.CronExpression);
            }
        }
    }

    private async Task ExecuteJobScoped(Models.CronJob job, CancellationToken token)
    {
        try
        {
            // We create a scope so that the task can use Scoped services (e.g., DbContext)
            using var scope = _serviceProvider.CreateScope();
            await job.Action(scope.ServiceProvider, token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing Cron task: {Cron}", job.CronExpression);
        }
    }
}