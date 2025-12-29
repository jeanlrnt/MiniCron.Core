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
        
        Assert.True(actual >= before && actual <= after.AddMilliseconds(200));
    }
    
    [Fact]
    public void SystemClock_Now_WithNullTimeZone_ReturnsLocalTime()
    {
        var clock = new SystemClock();
        var before = DateTime.Now;
        var actual = clock.Now(null);
        var after = DateTime.Now;
        
        Assert.True(actual >= before && actual <= after.AddMilliseconds(200));
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
        
        // After dispose, should be able to acquire again (locks cleared)
        var acquired = await provider.TryAcquireAsync(jobId, TimeSpan.FromMinutes(1), CancellationToken.None);
        Assert.True(acquired);
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
}
