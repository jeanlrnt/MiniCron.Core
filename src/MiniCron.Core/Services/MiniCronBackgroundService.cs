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
    private readonly bool _ownsLockProvider;
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
        
        if (lockProvider == null)
        {
            _lockProvider = new InMemoryJobLockProvider();
            _ownsLockProvider = true;
        }
        else
        {
            _lockProvider = lockProvider;
            _ownsLockProvider = false;
        }
        
        _concurrencySemaphore = new SemaphoreSlim(Math.Max(1, _options.MaxConcurrency));
    }

    // Backwards-compatible constructor for callers that instantiate/register the hosted
    // service without providing IOptions or ISystemClock via DI.
    // Checks if these services are available in the serviceProvider before creating new instances.
    // Always passes null for IJobLockProvider to maintain backwards compatibility (creates and owns its own instance).
    public MiniCronBackgroundService(
        JobRegistry registry,
        IServiceProvider serviceProvider,
        ILogger<MiniCronBackgroundService> logger)
        : this(
            registry, 
            serviceProvider, 
            logger, 
            ResolveOptions(serviceProvider),
            ResolveClock(serviceProvider),
            null)
    {
    }

    private static IOptions<MiniCronOptions> ResolveOptions(IServiceProvider serviceProvider)
    {
        return serviceProvider.GetService<IOptions<MiniCronOptions>>() ?? Options.Create(new MiniCronOptions());
    }

    private static ISystemClock ResolveClock(IServiceProvider serviceProvider)
    {
        return serviceProvider.GetService<ISystemClock>() ?? new SystemClock();
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
                            var semaphoreAcquired = false;
                            var lockAcquired = false;
                            try
                            {
                                // Attempt to acquire distributed lock first (TTL = job timeout or default)
                                var ttl = job.Timeout ?? _options.DefaultJobTimeout ?? TimeSpan.FromMinutes(30);
                                lockAcquired = await _lockProvider.TryAcquireAsync(job.Id, ttl, stoppingToken);

                                if (!lockAcquired)
                                {
                                    _logger.LogWarning("Could not acquire lock for job {JobId}, skipping", job.Id);
                                    _runningJobs.TryRemove(job.Id, out _);
                                    return;
                                }

                                try
                                {
                                    // Acquire concurrency semaphore after getting the lock
                                    await _concurrencySemaphore.WaitAsync(stoppingToken);
                                    semaphoreAcquired = true;

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
                                        // Release concurrency slot only if it was acquired
                                        if (semaphoreAcquired)
                                        {
                                            try
                                            {
                                                _concurrencySemaphore.Release();
                                            }
                                            catch (ObjectDisposedException)
                                            {
                                                // Service is being disposed, ignore
                                            }
                                        }
                                    }
                                }
                                finally
                                {
                                    // Release distributed lock only if it was acquired
                                    if (lockAcquired)
                                    {
                                        try
                                        {
                                            await _lockProvider.ReleaseAsync(job.Id);
                                        }
                                        catch (ObjectDisposedException)
                                        {
                                            // Service is being disposed, ignore
                                        }
                                    }
                                }
                            }
                            catch (OperationCanceledException)
                            {
                                sw.Stop();
                                _logger.LogWarning("Job {JobId} cancelled after {ElapsedMs}ms", job.Id, sw.ElapsedMilliseconds);
                            }
                            catch (ObjectDisposedException)
                            {
                                // Service is being disposed, ignore this job execution
                                sw.Stop();
                            }
                            catch (Exception ex)
                            {
                                sw.Stop();
                                _logger.LogError(ex, "Job {JobId} failed after {ElapsedMs}ms", job.Id, sw.ElapsedMilliseconds);
                            }
                            finally
                            {
                                // Ensure stopwatch is stopped
                                if (sw.IsRunning)
                                {
                                    sw.Stop();
                                }
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
        if (_ownsLockProvider && _lockProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }

        _concurrencySemaphore?.Dispose();
        
        base.Dispose();
    }
}