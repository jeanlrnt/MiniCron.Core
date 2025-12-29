using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MiniCron.Core.Helpers;
using MiniCron.Core.Models;
using System.Collections.Concurrent;

namespace MiniCron.Core.Services;

public class MiniCronBackgroundService : BackgroundService
{
    private readonly JobRegistry _registry;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MiniCronBackgroundService> _logger;
    private readonly ConcurrentDictionary<Guid, byte> _runningJobs = new();
    private readonly MiniCronOptions _options;
    private readonly ISystemClock _clock;
    private readonly IJobLockProvider _lockProvider;
    private readonly SemaphoreSlim _concurrencySemaphore;

    public MiniCronBackgroundService(
        JobRegistry registry,
        IServiceProvider serviceProvider,
        ILogger<MiniCronBackgroundService> logger,
        IOptions<MiniCronOptions>? options,
        ISystemClock clock,
        IJobLockProvider? lockProvider = null)
    {
        _registry = registry;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = options?.Value ?? new MiniCronOptions();
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _lockProvider = lockProvider ?? new InMemoryJobLockProvider();
        _concurrencySemaphore = new SemaphoreSlim(Math.Max(1, _options.MaxConcurrency));
    }

    // Backwards-compatible constructor for callers that instantiate/register the hosted
    // service without providing IOptions or ISystemClock via DI.
    public MiniCronBackgroundService(
        JobRegistry registry,
        IServiceProvider serviceProvider,
        ILogger<MiniCronBackgroundService> logger)
        : this(registry, serviceProvider, logger, Options.Create(new MiniCronOptions()), new SystemClock())
    {
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Align to configured granularity boundary
        var now = _clock.Now(_options.TimeZone);

        switch (_options.Granularity)
        {
            case CronGranularity.Minute:
            {
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

                break;
            }
            case CronGranularity.Second:
            {
                var delayToNextSecond = 1000 - now.Millisecond;
                if (delayToNextSecond > 0)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(delayToNextSecond), stoppingToken);
                }

                using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

                await RunJobs(stoppingToken);

                while (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    await RunJobs(stoppingToken);
                }

                break;
            }
            default:
                throw new InvalidOperationException($"Unsupported Cron granularity: {_options.Granularity}");
        }
    }

    private Task RunJobs(CancellationToken stoppingToken)
    {
        var now = _clock.Now(_options.TimeZone);

        foreach (var job in _registry.GetJobs())
        {
            try
            {
                if (CronHelper.IsDue(job.CronExpression, now))
                {
                    // Check if this job is already running to prevent duplicate executions
                    if (_runningJobs.TryAdd(job.Id, 0))
                    {
                        // Dispatch the job. Keep using Task.Run for now. log lifecycle.
                        _logger.LogInformation("Dispatching job {JobId} {Cron}", job.Id, job.CronExpression);
                        _ = Task.Run(async () =>
                        {
                            var sw = System.Diagnostics.Stopwatch.StartNew();
                            _logger.LogInformation("Job started {JobId}", job.Id);
                            try
                            {
                                // Attempt to acquire distributed lock (TTL = job timeout or default)
                                var ttl = job.Timeout ?? _options.DefaultJobTimeout ?? TimeSpan.FromMinutes(30);
                                var acquired = await _lockProvider.TryAcquireAsync(job.Id, ttl, stoppingToken);

                                if (!acquired)
                                {
                                    _logger.LogWarning("Could not acquire lock for job {JobId}, skipping", job.Id);
                                    return;
                                }

                                try
                                {
                                    // Acquire concurrency semaphore after lock is acquired
                                    await _concurrencySemaphore.WaitAsync(stoppingToken);

                                    try
                                    {
                                        // Execute with timeout enforcement
                                        var jobTimeout = job.Timeout ?? _options.DefaultJobTimeout;
                                        if (jobTimeout.HasValue)
                                        {
                                            using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                                            cts.CancelAfter(jobTimeout.Value);
                                            await ExecuteJobScoped(job, cts.Token);
                                        }
                                        else
                                        {
                                            await ExecuteJobScoped(job, stoppingToken);
                                        }

                                        sw.Stop();
                                        _logger.LogInformation("Job completed {JobId} in {ElapsedMs}ms", job.Id, sw.ElapsedMilliseconds);
                                    }
                                    finally
                                    {
                                        // Release concurrency slot
                                        try
                                        {
                                            _concurrencySemaphore.Release();
                                        }
                                        catch
                                        {
                                            _logger.LogError("Error releasing concurrency semaphore for job {JobId}", job.Id);
                                        }
                                    }
                                }
                                finally
                                {
                                    await _lockProvider.ReleaseAsync(job.Id);
                                }
                            }
                            catch (OperationCanceledException)
                            {
                                _logger.LogWarning("Job {JobId} cancelled", job.Id);
                            }
                            catch (Exception ex)
                            {
                                sw.Stop();
                                _logger.LogError(ex, "Job {JobId} failed after {ElapsedMs}ms", job.Id, sw.ElapsedMilliseconds);
                            }
                            finally
                            {
                                // Remove from running jobs when complete
                                _runningJobs.TryRemove(job.Id, out _);
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

        return Task.CompletedTask;
    }

    private async Task ExecuteJobScoped(CronJob job, CancellationToken token)
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

    public override void Dispose()
    {
        _concurrencySemaphore.Dispose();
        base.Dispose();
    }
}