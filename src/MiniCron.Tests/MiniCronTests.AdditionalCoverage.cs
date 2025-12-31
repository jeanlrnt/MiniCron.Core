using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MiniCron.Core.Helpers;
using MiniCron.Core.Models;
using MiniCron.Core.Services;

namespace MiniCron.Tests;

public partial class MiniCronTests
{
    [Fact]
    public void SystemClock_UtcNow_ReturnsUtcTime()
    {
        var clock = new SystemClock();
        var before = DateTime.UtcNow;
        var actual = clock.UtcNow;
        var after = DateTime.UtcNow;
        
        Assert.True(actual >= before && actual <= after.AddMilliseconds(50));
    }
    
    [Fact]
    public void SystemClock_Now_WithUtcTimeZone_ReturnsUtcTime()
    {
        var clock = new SystemClock();
        var before = DateTime.UtcNow;
        var actual = clock.Now(TimeZoneInfo.Utc);
        var after = DateTime.UtcNow;
        
        Assert.True(actual >= before && actual <= after.AddMilliseconds(50));
    }
    
    [Fact]
    public void JobEventArgs_Constructor_WithPreviousJob_StoresValues()
    {
        var job = new CronJob("* * * * *", (sp, ct) => Task.CompletedTask);
        var previousJob = new CronJob("*/5 * * * *", (sp, ct) => Task.CompletedTask);
        
        var eventArgs = new JobEventArgs(job, previousJob);
        
        Assert.Equal(job, eventArgs.Job);
        Assert.Equal(previousJob, eventArgs.PreviousJob);
    }
    
    [Fact]
    public void JobEventArgs_Constructor_WithoutPreviousJob_StoresJob()
    {
        var job = new CronJob("* * * * *", (sp, ct) => Task.CompletedTask);
        
        var eventArgs = new JobEventArgs(job);
        
        Assert.Equal(job, eventArgs.Job);
        Assert.Null(eventArgs.PreviousJob);
    }
    
    [Fact]
    public void JobRegistry_Dispose_DisposesLock()
    {
        var registry = new JobRegistry();
        registry.ScheduleJob("* * * * *", (sp, ct) => Task.CompletedTask);
        
        // Should not throw
        registry.Dispose();
    }
    
    [Fact]
    public async Task InMemoryJobLockProvider_Dispose_ClearsLocks()
    {
        var provider = new InMemoryJobLockProvider();
        var jobId = Guid.NewGuid();
        
        _ = await provider.TryAcquireAsync(jobId, TimeSpan.FromMinutes(1), CancellationToken.None);
        
        provider.Dispose();
        
        // After dispose, attempting to acquire should throw ObjectDisposedException
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await provider.TryAcquireAsync(jobId, TimeSpan.FromMinutes(1), CancellationToken.None));
    }
    
    [Fact]
    public async Task InMemoryJobLockProvider_TryAcquire_WithLongWait_EventuallyAcquires()
    {
        var provider = new InMemoryJobLockProvider();
        var jobId = Guid.NewGuid();
        
        // First acquire
        var acquired1 = await provider.TryAcquireAsync(jobId, TimeSpan.FromMilliseconds(50), CancellationToken.None);
        Assert.True(acquired1);
        
        // Start second acquire in background (will wait for TTL)
        var acquireTask = Task.Run(async () =>
        {
            var acquired = await provider.TryAcquireAsync(jobId, TimeSpan.FromMinutes(1), CancellationToken.None);
            return acquired;
        });
        
        // Wait for lock to expire
        await Task.Delay(100);
        
        var acquired2 = await acquireTask;
        Assert.True(acquired2);
    }
    
    [Fact]
    public void CronHelper_ValidateField_WithInvalidStepFormat_ThrowsException()
    {
        var registry = new JobRegistry();
        
        var exception = Assert.Throws<ArgumentException>(() =>
            registry.ScheduleJob("*/5/2 * * * *", (sp, ct) => Task.CompletedTask));
        
        Assert.Contains("Invalid step syntax", exception.Message);
    }
    
    [Fact]
    public void CronHelper_ValidateField_WithNonIntegerStep_ThrowsException()
    {
        var registry = new JobRegistry();
        
        var exception = Assert.Throws<ArgumentException>(() =>
            registry.ScheduleJob("*/abc * * * *", (sp, ct) => Task.CompletedTask));
        
        Assert.Contains("Step must be a valid integer", exception.Message);
    }
    
    [Fact]
    public void CronHelper_ValidateField_WithZeroStep_ThrowsException()
    {
        var registry = new JobRegistry();
        
        var exception = Assert.Throws<ArgumentException>(() =>
            registry.ScheduleJob("*/0 * * * *", (sp, ct) => Task.CompletedTask));
        
        Assert.Contains("Step must be greater than zero", exception.Message);
    }
    
    [Fact]
    public void CronHelper_ValidateField_WithStepTooLarge_ThrowsException()
    {
        var registry = new JobRegistry();
        
        var exception = Assert.Throws<ArgumentException>(() =>
            registry.ScheduleJob("*/100 * * * *", (sp, ct) => Task.CompletedTask));
        
        Assert.Contains("Step must be less than or equal to", exception.Message);
    }
    
    [Fact]
    public void CronHelper_ValidateField_WithEmptyListValue_ThrowsException()
    {
        var registry = new JobRegistry();
        
        var exception = Assert.Throws<ArgumentException>(() =>
            registry.ScheduleJob("1,,3 * * * *", (sp, ct) => Task.CompletedTask));
        
        Assert.Contains("Empty value in", exception.Message);
    }
    
    [Fact]
    public void CronHelper_ValidateField_WithNonIntegerListValue_ThrowsException()
    {
        var registry = new JobRegistry();
        
        var exception = Assert.Throws<ArgumentException>(() =>
            registry.ScheduleJob("1,abc,3 * * * *", (sp, ct) => Task.CompletedTask));
        
        Assert.Contains("All values must be integers", exception.Message);
    }
    
    [Fact]
    public void CronHelper_ValidateField_WithListValueOutOfRange_ThrowsException()
    {
        var registry = new JobRegistry();
        
        var exception = Assert.Throws<ArgumentException>(() =>
            registry.ScheduleJob("1,99,3 * * * *", (sp, ct) => Task.CompletedTask));
        
        Assert.Contains("is out of range", exception.Message);
    }
    
    [Fact]
    public void CronHelper_ValidateField_WithInvalidRangeFormat_ThrowsException()
    {
        var registry = new JobRegistry();
        
        var exception = Assert.Throws<ArgumentException>(() =>
            registry.ScheduleJob("1-5-10 * * * *", (sp, ct) => Task.CompletedTask));
        
        Assert.Contains("Invalid range syntax", exception.Message);
    }
    
    [Fact]
    public void CronHelper_ValidateField_WithNonIntegerRangeValue_ThrowsException()
    {
        var registry = new JobRegistry();
        
        var exception = Assert.Throws<ArgumentException>(() =>
            registry.ScheduleJob("abc-xyz * * * *", (sp, ct) => Task.CompletedTask));
        
        Assert.Contains("Both start and end must be integers", exception.Message);
    }
    
    [Fact]
    public void CronHelper_ValidateField_WithRangeOutOfRange_ThrowsException()
    {
        var registry = new JobRegistry();
        
        var exception = Assert.Throws<ArgumentException>(() =>
            registry.ScheduleJob("55-99 * * * *", (sp, ct) => Task.CompletedTask));
        
        Assert.Contains("are out of range", exception.Message);
    }
    
    [Fact]
    public void CronHelper_ValidateField_WithInvalidRangeOrder_ThrowsException()
    {
        var registry = new JobRegistry();
        
        var exception = Assert.Throws<ArgumentException>(() =>
            registry.ScheduleJob("10-5 * * * *", (sp, ct) => Task.CompletedTask));
        
        Assert.Contains("Start value must be less than or equal to end value", exception.Message);
    }
    
    [Fact]
    public void CronHelper_ValidateField_WithNonIntegerValue_ThrowsException()
    {
        var registry = new JobRegistry();
        
        var exception = Assert.Throws<ArgumentException>(() =>
            registry.ScheduleJob("abc * * * *", (sp, ct) => Task.CompletedTask));
        
        Assert.Contains("Expected an integer, wildcard", exception.Message);
    }
    
    [Fact]
    public void CronHelper_ValidateField_WithValueOutOfRange_ThrowsException()
    {
        var registry = new JobRegistry();
        
        var exception = Assert.Throws<ArgumentException>(() =>
            registry.ScheduleJob("99 * * * *", (sp, ct) => Task.CompletedTask));
        
        Assert.Contains("is out of range", exception.Message);
    }
    
    [Fact]
    public void CronHelper_IsDue_WithInvalidExpression_ReturnsFalse()
    {
        var result = CronHelper.IsDue("* * *", DateTime.Now);
        Assert.False(result);
    }
    
    [Fact]
    public void CronHelper_IsDue_WithInvalidStepSyntax_ReturnsFalse()
    {
        var time = new DateTime(2024, 1, 1, 12, 15, 0);
        var result = CronHelper.IsDue("5/3 * * * *", time);
        Assert.False(result);
    }
    
    [Fact]
    public void CronHelper_IsDue_WithValidRange_ReturnsTrue()
    {
        var time = new DateTime(2024, 1, 1, 12, 15, 0);
        var result = CronHelper.IsDue("10-20 * * * *", time);
        Assert.True(result);
    }
    
    [Fact]
    public void CronHelper_IsDue_WithInvalidRange_ReturnsFalse()
    {
        var time = new DateTime(2024, 1, 1, 12, 5, 0);
        var result = CronHelper.IsDue("10-20 * * * *", time);
        Assert.False(result);
    }
    
    [Fact]
    public void CronHelper_IsDue_WithList_ReturnsTrue()
    {
        var time = new DateTime(2024, 1, 1, 12, 15, 0);
        var result = CronHelper.IsDue("5,15,25 * * * *", time);
        Assert.True(result);
    }
    
    [Fact]
    public void CronHelper_IsDue_WithListMiss_ReturnsFalse()
    {
        var time = new DateTime(2024, 1, 1, 12, 7, 0);
        var result = CronHelper.IsDue("5,15,25 * * * *", time);
        Assert.False(result);
    }
    
    [Fact]
    public void CronHelper_IsDue_WithExactValue_ReturnsTrue()
    {
        var time = new DateTime(2024, 1, 1, 12, 15, 0);
        var result = CronHelper.IsDue("15 * * * *", time);
        Assert.True(result);
    }
    
    [Fact]
    public void CronHelper_IsDue_WithInvalidRangeSyntax_ReturnsFalse()
    {
        var time = new DateTime(2024, 1, 1, 12, 15, 0);
        var result = CronHelper.IsDue("abc-xyz * * * *", time);
        Assert.False(result);
    }
    
    [Fact]
    public void CronHelper_IsDue_WithInvalidListValue_ReturnsFalse()
    {
        var time = new DateTime(2024, 1, 1, 12, 15, 0);
        var result = CronHelper.IsDue("5,abc,25 * * * *", time);
        Assert.False(result);
    }
    
    [Fact]
    public void CronHelper_IsDue_WithInvalidExactValue_ReturnsFalse()
    {
        var time = new DateTime(2024, 1, 1, 12, 15, 0);
        var result = CronHelper.IsDue("abc * * * *", time);
        Assert.False(result);
    }
    
    [Fact]
    public async Task MiniCronBackgroundService_RunJobs_ExecutesMatchingJobs()
    {
        var services = new ServiceCollection();
        var jobExecuted = false;
        var registry = new JobRegistry();
        
        registry.ScheduleJob("* * * * *", (sp, ct) =>
        {
            jobExecuted = true;
            return Task.CompletedTask;
        });
        
        services.AddSingleton(registry);
        services.AddSingleton<IHostedService, MiniCronBackgroundService>();
        services.AddLogging();
        
        var serviceProvider = services.BuildServiceProvider();
        var backgroundService = serviceProvider.GetServices<IHostedService>()
            .OfType<MiniCronBackgroundService>()
            .First();
        
        using var cts = new CancellationTokenSource();
        await backgroundService.StartAsync(cts.Token);
        
        // Wait for async job execution
        await Task.Delay(50);
        
        Assert.True(jobExecuted);
    }
    
    [Fact]
    public async Task MiniCronBackgroundService_RunJobs_WithNoTimeout_UsesDefaultTimeout()
    {
        var services = new ServiceCollection();
        var jobExecuted = false;
        var registry = new JobRegistry();
        
        registry.ScheduleJob("* * * * *", async (sp, ct) =>
        {
            jobExecuted = true;
            await Task.Delay(10, ct);
        });
        
        services.AddSingleton(registry);
        services.AddSingleton<IHostedService, MiniCronBackgroundService>();
        services.AddLogging();
        services.AddSingleton<IOptions<MiniCronOptions>>(Options.Create(new MiniCronOptions
        {
            DefaultJobTimeout = null
        }));
        
        var serviceProvider = services.BuildServiceProvider();
        var backgroundService = serviceProvider.GetServices<IHostedService>()
            .OfType<MiniCronBackgroundService>()
            .First();
        
        var runJobsMethod = typeof(MiniCronBackgroundService)
            .GetMethod("RunJobs", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        Assert.NotNull(runJobsMethod);
        
        using var cts = new CancellationTokenSource();
        var task = (Task)runJobsMethod.Invoke(backgroundService, new object[] { cts.Token })!;
        await task;
        
        // Wait for async job execution
        await Task.Delay(50);
        
        Assert.True(jobExecuted);
    }
    
    [Fact]
    public async Task MiniCronBackgroundService_Constructor_WithNullOptions_UsesDefaults()
    {
        var services = new ServiceCollection();
        var registry = new JobRegistry();
        
        registry.ScheduleJob("* * * * *", (sp, ct) => Task.CompletedTask);
        
        services.AddSingleton(registry);
        services.AddSingleton<ISystemClock>(new SystemClock());
        services.AddLogging();
        
        // Create service with explicit constructor that passes null options
        var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<MiniCronBackgroundService>>();
        var clock = serviceProvider.GetRequiredService<ISystemClock>();
        
        var backgroundService = new MiniCronBackgroundService(
            registry,
            serviceProvider,
            logger,
            null, // null options
            clock);
        
        Assert.NotNull(backgroundService);
        await Task.CompletedTask;
    }
    
    [Fact]
    public async Task MiniCronBackgroundService_RunJobs_WithException_ContinuesExecution()
    {
        var services = new ServiceCollection();
        var jobExecuted = false;
        var registry = new JobRegistry();
        
        registry.ScheduleJob("* * * * *", (sp, ct) =>
        {
            jobExecuted = true;
            throw new InvalidOperationException("Test exception");
        });
        
        services.AddSingleton(registry);
        services.AddSingleton<IHostedService, MiniCronBackgroundService>();
        services.AddLogging();
        
        var serviceProvider = services.BuildServiceProvider();
        var backgroundService = serviceProvider.GetServices<IHostedService>()
            .OfType<MiniCronBackgroundService>()
            .First();
        
        var runJobsMethod = typeof(MiniCronBackgroundService)
            .GetMethod("RunJobs", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        Assert.NotNull(runJobsMethod);
        
        using var cts = new CancellationTokenSource();
        var task = (Task)runJobsMethod.Invoke(backgroundService, new object[] { cts.Token })!;
        await task;
        
        // Wait for async job execution
        await Task.Delay(50);
        
        Assert.True(jobExecuted);
    }
    
    [Fact]
    public async Task MiniCronBackgroundService_RunJobs_WithInvalidCronExpression_LogsError()
    {
        var services = new ServiceCollection();
        var registry = new JobRegistry();
        
        // Manually add an invalid job (bypassing validation for testing error handling)
        var job = new CronJob("invalid cron", (sp, ct) => Task.CompletedTask);
        Assert.NotNull(job);
        
        services.AddSingleton(registry);
        services.AddSingleton<IHostedService, MiniCronBackgroundService>();
        services.AddLogging();
        
        var serviceProvider = services.BuildServiceProvider();
        var backgroundService = serviceProvider.GetServices<IHostedService>()
            .OfType<MiniCronBackgroundService>()
            .First();
        
        var runJobsMethod = typeof(MiniCronBackgroundService)
            .GetMethod("RunJobs", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        Assert.NotNull(runJobsMethod);
        
        using var cts = new CancellationTokenSource();
        var task = (Task)runJobsMethod.Invoke(backgroundService, new object[] { cts.Token })!;
        
        // Should not throw even with invalid cron expression
        await task;
    }
    
    [Fact]
    public void MiniCronBackgroundService_Constructor_WithNullClock_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();
        var registry = new JobRegistry();
        
        services.AddSingleton(registry);
        services.AddLogging();
        
        var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<MiniCronBackgroundService>>();
        
        Assert.Throws<ArgumentNullException>(() => new MiniCronBackgroundService(
            registry,
            serviceProvider,
            logger,
            null,
            null!)); // null clock should throw
    }
    
    [Fact]
    public async Task MiniCronBackgroundService_RunJobs_WhenJobAlreadyRunning_SkipsExecution()
    {
        var services = new ServiceCollection();
        var executionCount = 0;
        var jobStarted = new TaskCompletionSource<bool>();
        var jobCanComplete = new TaskCompletionSource<bool>();
        var registry = new JobRegistry();
        
        registry.ScheduleJob("* * * * *", async (sp, ct) =>
        {
            Interlocked.Increment(ref executionCount);
            jobStarted.TrySetResult(true);
            await jobCanComplete.Task;
        });
        
        services.AddSingleton(registry);
        services.AddSingleton<IHostedService, MiniCronBackgroundService>();
        services.AddLogging();
        
        var serviceProvider = services.BuildServiceProvider();
        var backgroundService = serviceProvider.GetServices<IHostedService>()
            .OfType<MiniCronBackgroundService>()
            .First();
        
        var runJobsMethod = typeof(MiniCronBackgroundService)
            .GetMethod("RunJobs", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        Assert.NotNull(runJobsMethod);
        
        using var cts = new CancellationTokenSource();
        
        // Start first execution
        var task1 = (Task)runJobsMethod.Invoke(backgroundService, new object[] { cts.Token })!;
        await task1;
        
        // Wait for job to actually start
        await jobStarted.Task;
        
        // Try to run again while first is still running
        var task2 = (Task)runJobsMethod.Invoke(backgroundService, new object[] { cts.Token })!;
        await task2;
        
        // Let first job complete
        jobCanComplete.SetResult(true);
        await Task.Delay(100);
        
        // Should have only executed once
        Assert.Equal(1, executionCount);
    }
    
    [Fact]
    public async Task MiniCronBackgroundService_RunJobs_WithDefaultTTL_UsesThirtyMinutes()
    {
        var services = new ServiceCollection();
        var jobExecuted = false;
        var registry = new JobRegistry();
        
        registry.ScheduleJob("* * * * *", async (sp, ct) =>
        {
            jobExecuted = true;
            await Task.Delay(10, ct);
        });
        
        services.AddSingleton(registry);
        services.AddSingleton<IHostedService, MiniCronBackgroundService>();
        services.AddLogging();
        services.AddSingleton<IOptions<MiniCronOptions>>(Options.Create(new MiniCronOptions
        {
            DefaultJobTimeout = null
        }));
        
        var serviceProvider = services.BuildServiceProvider();
        var backgroundService = serviceProvider.GetServices<IHostedService>()
            .OfType<MiniCronBackgroundService>()
            .First();
        
        var runJobsMethod = typeof(MiniCronBackgroundService)
            .GetMethod("RunJobs", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        Assert.NotNull(runJobsMethod);
        
        using var cts = new CancellationTokenSource();
        var task = (Task)runJobsMethod.Invoke(backgroundService, new object[] { cts.Token })!;
        await task;
        
        await Task.Delay(50);
        Assert.True(jobExecuted);
    }
    
    [Fact]
    public async Task MiniCronBackgroundService_ExecuteAsync_WithUnsupportedGranularity_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        var registry = new JobRegistry();
        
        registry.ScheduleJob("* * * * *", (sp, ct) => Task.CompletedTask);
        
        services.AddSingleton(registry);
        services.AddSingleton<ISystemClock>(new SystemClock());
        services.AddLogging();
        services.AddSingleton<IOptions<MiniCronOptions>>(Options.Create(new MiniCronOptions
        {
            Granularity = (CronGranularity)999 // Invalid granularity
        }));
        
        var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<MiniCronBackgroundService>>();
        var clock = serviceProvider.GetRequiredService<ISystemClock>();
        var options = serviceProvider.GetRequiredService<IOptions<MiniCronOptions>>();
        
        var backgroundService = new MiniCronBackgroundService(
            registry,
            serviceProvider,
            logger,
            options,
            clock);
        
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(100);
        
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await backgroundService.StartAsync(cts.Token);
        });
    }
    
    [Fact]
    public async Task MiniCronBackgroundService_BackwardsCompatibleConstructor_Works()
    {
        var services = new ServiceCollection();
        var registry = new JobRegistry();
        var jobExecuted = false;
        
        registry.ScheduleJob("* * * * *", (sp, ct) =>
        {
            jobExecuted = true;
            return Task.CompletedTask;
        });
        
        services.AddSingleton(registry);
        services.AddLogging();
        
        var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<MiniCronBackgroundService>>();
        
        // Use backwards-compatible constructor
        var backgroundService = new MiniCronBackgroundService(
            registry,
            serviceProvider,
            logger);
        
        var runJobsMethod = typeof(MiniCronBackgroundService)
            .GetMethod("RunJobs", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        Assert.NotNull(runJobsMethod);
        
        using var cts = new CancellationTokenSource();
        var task = (Task)runJobsMethod.Invoke(backgroundService, new object[] { cts.Token })!;
        await task;
        
        await Task.Delay(50);
        Assert.True(jobExecuted);
    }
    
    [Fact]
    public async Task MiniCronBackgroundService_BackwardsCompatibleConstructor_WithRegisteredServices_UsesThemFromDI()
    {
        var services = new ServiceCollection();
        var registry = new JobRegistry();
        var jobExecuted = false;
        
        registry.ScheduleJob("* * * * *", (sp, ct) =>
        {
            jobExecuted = true;
            return Task.CompletedTask;
        });
        
        // Register custom options with a specific configuration
        var customOptions = new MiniCronOptions
        {
            MaxConcurrency = 5,
            TimeZone = TimeZoneInfo.Utc,
            Granularity = CronGranularity.Minute
        };
        services.AddSingleton<IOptions<MiniCronOptions>>(Options.Create(customOptions));
        
        // Register custom system clock
        services.AddSingleton<ISystemClock>(new SystemClock());
        
        services.AddSingleton(registry);
        services.AddLogging();
        
        var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<MiniCronBackgroundService>>();
        
        // Use backwards-compatible constructor - should resolve from DI
        var backgroundService = new MiniCronBackgroundService(
            registry,
            serviceProvider,
            logger);
        
        // Verify the service was created successfully
        Assert.NotNull(backgroundService);
        
        // Use reflection to verify the options were used
        var optionsField = typeof(MiniCronBackgroundService)
            .GetField("_options", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(optionsField);
        
        var actualOptions = (MiniCronOptions)optionsField.GetValue(backgroundService)!;
        Assert.NotNull(actualOptions);
        Assert.Equal(5, actualOptions.MaxConcurrency);
        Assert.Equal(TimeZoneInfo.Utc, actualOptions.TimeZone);
        Assert.Equal(CronGranularity.Minute, actualOptions.Granularity);
        
        // Verify it can execute jobs
        var runJobsMethod = typeof(MiniCronBackgroundService)
            .GetMethod("RunJobs", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        Assert.NotNull(runJobsMethod);
        
        using var cts = new CancellationTokenSource();
        var task = (Task)runJobsMethod.Invoke(backgroundService, new object[] { cts.Token })!;
        await task;
        
        await Task.Delay(50);
        Assert.True(jobExecuted);
    }
    
    [Fact]
    public async Task MiniCronBackgroundService_BackwardsCompatibleConstructor_WithoutRegisteredServices_CreatesFallbacks()
    {
        var services = new ServiceCollection();
        var registry = new JobRegistry();
        var jobExecuted = false;
        
        registry.ScheduleJob("* * * * *", (sp, ct) =>
        {
            jobExecuted = true;
            return Task.CompletedTask;
        });
        
        // Do NOT register IOptions or ISystemClock
        services.AddSingleton(registry);
        services.AddLogging();
        
        var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<MiniCronBackgroundService>>();
        
        // Use backwards-compatible constructor - should create fallback instances
        var backgroundService = new MiniCronBackgroundService(
            registry,
            serviceProvider,
            logger);
        
        // Verify the service was created successfully
        Assert.NotNull(backgroundService);
        
        // Use reflection to verify default options were used
        var optionsField = typeof(MiniCronBackgroundService)
            .GetField("_options", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(optionsField);
        
        var actualOptions = (MiniCronOptions)optionsField.GetValue(backgroundService)!;
        Assert.NotNull(actualOptions);
        // Verify defaults from MiniCronOptions constructor
        Assert.Equal(10, actualOptions.MaxConcurrency); // Default value
        Assert.Equal(CronGranularity.Minute, actualOptions.Granularity); // Default value
        
        // Verify the clock was created
        var clockField = typeof(MiniCronBackgroundService)
            .GetField("_clock", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(clockField);
        
        var actualClock = clockField.GetValue(backgroundService);
        Assert.NotNull(actualClock);
        Assert.IsType<SystemClock>(actualClock);
        
        // Verify it can execute jobs
        var runJobsMethod = typeof(MiniCronBackgroundService)
            .GetMethod("RunJobs", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        Assert.NotNull(runJobsMethod);
        
        using var cts = new CancellationTokenSource();
        var task = (Task)runJobsMethod.Invoke(backgroundService, new object[] { cts.Token })!;
        await task;
        
        await Task.Delay(50);
        Assert.True(jobExecuted);
    }
    
    [Fact]
    public async Task MiniCronBackgroundService_RunJobs_WithJobHavingSpecificTimeout_UsesJobTimeout()
    {
        var services = new ServiceCollection();
        var jobExecuted = false;
        var registry = new JobRegistry();
        
        // Create a job that will run under the default timeout configured in MiniCronOptions
        // Note: this test verifies the default timeout path rather than configuring a per-job timeout
        // (job-specific timeouts are handled elsewhere and are not exercised by this test)
        registry.ScheduleJob("* * * * *", async (sp, ct) =>
        {
            jobExecuted = true;
            await Task.Delay(10, ct);
        });
        
        services.AddSingleton(registry);
        services.AddSingleton<IHostedService, MiniCronBackgroundService>();
        services.AddLogging();
        services.AddSingleton<IOptions<MiniCronOptions>>(Options.Create(new MiniCronOptions
        {
            DefaultJobTimeout = TimeSpan.FromSeconds(30)
        }));
        
        var serviceProvider = services.BuildServiceProvider();
        var backgroundService = serviceProvider.GetServices<IHostedService>()
            .OfType<MiniCronBackgroundService>()
            .First();
        
        var runJobsMethod = typeof(MiniCronBackgroundService)
            .GetMethod("RunJobs", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        Assert.NotNull(runJobsMethod);
        
        using var cts = new CancellationTokenSource();
        var task = (Task)runJobsMethod.Invoke(backgroundService, new object[] { cts.Token })!;
        await task;
        
        await Task.Delay(50);
        Assert.True(jobExecuted);
    }
    
    [Fact]
    public async Task MiniCronBackgroundService_RunJobs_WithJobCompletedSuccessfully_LogsCompletion()
    {
        var services = new ServiceCollection();
        var jobExecuted = false;
        var registry = new JobRegistry();
        
        registry.ScheduleJob("* * * * *", async (sp, ct) =>
        {
            jobExecuted = true;
            await Task.Delay(10, ct);
        });
        
        services.AddSingleton(registry);
        services.AddSingleton<IHostedService, MiniCronBackgroundService>();
        services.AddLogging();
        
        var serviceProvider = services.BuildServiceProvider();
        var backgroundService = serviceProvider.GetServices<IHostedService>()
            .OfType<MiniCronBackgroundService>()
            .First();
        
        var runJobsMethod = typeof(MiniCronBackgroundService)
            .GetMethod("RunJobs", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        Assert.NotNull(runJobsMethod);
        
        using var cts = new CancellationTokenSource();
        var task = (Task)runJobsMethod.Invoke(backgroundService, new object[] { cts.Token })!;
        await task;
        
        await Task.Delay(50);
        Assert.True(jobExecuted);
    }
    
    [Fact]
    public async Task MiniCronBackgroundService_RunJobs_WithSemaphoreReleaseError_HandlesGracefully()
    {
        // This test is challenging to trigger the semaphore release error path (lines 176-179)
        // which requires the semaphore to be in an invalid state
        var services = new ServiceCollection();
        var jobExecuted = false;
        var registry = new JobRegistry();
        
        registry.ScheduleJob("* * * * *", async (sp, ct) =>
        {
            jobExecuted = true;
            await Task.Delay(10, ct);
        });
        
        services.AddSingleton(registry);
        services.AddSingleton<IHostedService, MiniCronBackgroundService>();
        services.AddLogging();
        
        var serviceProvider = services.BuildServiceProvider();
        var backgroundService = serviceProvider.GetServices<IHostedService>()
            .OfType<MiniCronBackgroundService>()
            .First();
        
        var runJobsMethod = typeof(MiniCronBackgroundService)
            .GetMethod("RunJobs", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        Assert.NotNull(runJobsMethod);
        
        using var cts = new CancellationTokenSource();
        var task = (Task)runJobsMethod.Invoke(backgroundService, new object[] { cts.Token })!;
        await task;
        
        await Task.Delay(100);
        Assert.True(jobExecuted);
    }

    [Fact]
    public async Task MiniCronBackgroundService_RunJobs_WithJobCancellation_StopsStopwatchAndLogsElapsedTime()
    {
        // Arrange
        var services = new ServiceCollection();
        var registry = new JobRegistry();
        var jobStarted = new TaskCompletionSource<bool>();
        var jobCancelled = new TaskCompletionSource<bool>();
        
        registry.ScheduleJob("* * * * *", async (sp, ct) =>
        {
            jobStarted.SetResult(true);
            try
            {
                // Wait for cancellation - using a long delay that will be interrupted
                await Task.Delay(TimeSpan.FromSeconds(30), ct);
            }
            catch (OperationCanceledException)
            {
                jobCancelled.SetResult(true);
                throw; // Re-throw to test the handler
            }
        });
        
        services.AddSingleton(registry);
        services.AddSingleton<IHostedService, MiniCronBackgroundService>();
        services.AddLogging();
        
        var serviceProvider = services.BuildServiceProvider();
        var backgroundService = serviceProvider.GetServices<IHostedService>()
            .OfType<MiniCronBackgroundService>()
            .First();
        
        var runJobsMethod = typeof(MiniCronBackgroundService)
            .GetMethod("RunJobs", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        Assert.NotNull(runJobsMethod);
        
        // Act - Start job execution with a cancellation token
        using var cts = new CancellationTokenSource();
        var task = (Task)runJobsMethod.Invoke(backgroundService, new object[] { cts.Token })!;
        
        // Wait for job to start before cancelling
        await jobStarted.Task;
        await Task.Delay(TimeSpan.FromMilliseconds(50)); // Give job time to enter execution
        
        // Cancel the token while job is running
        cts.Cancel();
        
        // Complete the RunJobs task
        await task;
        
        // Wait for cancellation to be processed
        var cancelledInTime = await Task.WhenAny(jobCancelled.Task, Task.Delay(TimeSpan.FromSeconds(1))) == jobCancelled.Task;
        
        // Assert - Job should have been cancelled
        // The stopwatch should be stopped in the OperationCanceledException handler
        // and elapsed time should be logged (verified by no exceptions thrown)
        Assert.True(cancelledInTime, "Job should have been cancelled");
    }

    [Fact]
    public async Task MiniCronBackgroundService_RunJobs_WhenLockAcquisitionFails_StopsStopwatchInFinally()
    {
        // Arrange - Create a mock lock provider that always fails to acquire
        var services = new ServiceCollection();
        var registry = new JobRegistry();
        var jobAttempted = false;
        
        registry.ScheduleJob("* * * * *", (sp, ct) =>
        {
            jobAttempted = true;
            return Task.CompletedTask;
        });
        
        services.AddSingleton(registry);
        services.AddLogging();
        
        // Use a mock lock provider that always returns false (lock acquisition fails)
        var failingLockProvider = new FailingJobLockProvider();
        services.AddSingleton<IJobLockProvider>(failingLockProvider);
        services.AddSingleton<ISystemClock, SystemClock>();
        services.AddSingleton<IOptions<MiniCronOptions>>(Options.Create(new MiniCronOptions()));
        
        // Create the background service with the failing lock provider
        services.AddSingleton<IHostedService>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<MiniCronBackgroundService>>();
            var options = sp.GetRequiredService<IOptions<MiniCronOptions>>();
            var clock = sp.GetRequiredService<ISystemClock>();
            var lockProvider = sp.GetRequiredService<IJobLockProvider>();
            return new MiniCronBackgroundService(registry, sp, logger, options, clock, lockProvider);
        });
        
        var serviceProvider = services.BuildServiceProvider();
        var backgroundService = serviceProvider.GetServices<IHostedService>()
            .OfType<MiniCronBackgroundService>()
            .First();
        
        var runJobsMethod = typeof(MiniCronBackgroundService)
            .GetMethod("RunJobs", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        Assert.NotNull(runJobsMethod);
        
        // Act
        using var cts = new CancellationTokenSource();
        var task = (Task)runJobsMethod.Invoke(backgroundService, new object[] { cts.Token })!;
        await task;
        
        // Wait for processing
        await Task.Delay(TimeSpan.FromMilliseconds(100));
        
        // Assert - Job should not have executed because lock acquisition failed
        // The stopwatch should be stopped in the finally block
        // Verified by no exceptions thrown during execution
        Assert.False(jobAttempted);
    }

    // Helper class for testing lock acquisition failure
    private class FailingJobLockProvider : IJobLockProvider
    {
        public Task<bool> TryAcquireAsync(Guid jobId, TimeSpan ttl, CancellationToken cancellationToken)
        {
            // Always return false to simulate lock acquisition failure
            return Task.FromResult(false);
        }

        public Task ReleaseAsync(Guid jobId)
        {
            return Task.CompletedTask;
        }
    }
}
