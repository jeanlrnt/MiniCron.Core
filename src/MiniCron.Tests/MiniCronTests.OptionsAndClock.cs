using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MiniCron.Core.Models;
using MiniCron.Core.Services;
using MiniCron.Core.Extensions;

namespace MiniCron.Tests;

public partial class MiniCronTests
{
    [Fact]
    public void AddMiniCronOptions_RegistersServicesAndBindsOptions()
    {
        var services = new ServiceCollection();

        services.AddMiniCronOptions(opts =>
        {
            opts.MaxConcurrency = 3;
            opts.DefaultJobTimeout = TimeSpan.FromSeconds(30);
        });

        var sp = services.BuildServiceProvider();

        var registry = sp.GetService<JobRegistry>();
        Assert.NotNull(registry);

        var clock = sp.GetService<ISystemClock>();
        Assert.NotNull(clock);

        var lockProvider = sp.GetService<IJobLockProvider>();
        Assert.NotNull(lockProvider);

        var opts = sp.GetRequiredService<IOptions<MiniCronOptions>>().Value;
        Assert.Equal(3, opts.MaxConcurrency);
        Assert.Equal(TimeSpan.FromSeconds(30), opts.DefaultJobTimeout);
    }

    [Fact]
    public void SystemClock_Now_ReturnsConvertedTime()
    {
        var clock = new SystemClock();

        // Use UTC for deterministic comparison
        var before = DateTime.UtcNow;
        var actual = clock.Now(TimeZoneInfo.Utc);
        var after = DateTime.UtcNow;

        // actual should be between before and after (tolerance for small delays)
        Assert.True(actual >= before && actual <= after.AddMilliseconds(50));
    }

    [Fact]
    public void AddMiniCronOptions_InjectsLoggerIntoJobRegistry()
    {
        var services = new ServiceCollection();
        services.AddMiniCronOptions();

        var sp = services.BuildServiceProvider();
        var registry = sp.GetService<JobRegistry>();
        Assert.NotNull(registry);

        // Verify logger is working by scheduling a job and checking that it doesn't throw
        var jobId = registry.ScheduleJob("* * * * *", () => { });
        Assert.NotEqual(Guid.Empty, jobId);
        
        // Remove the job to trigger logger usage
        var removed = registry.RemoveJob(jobId);
        Assert.True(removed);
    }

    [Fact]
    public void AddMiniCron_InjectsLoggerIntoJobRegistry()
    {
        var services = new ServiceCollection();
        services.AddMiniCron(registry =>
        {
            // No-op configuration
        });

        var sp = services.BuildServiceProvider();
        var registry = sp.GetService<JobRegistry>();
        Assert.NotNull(registry);

        // Verify logger is working by scheduling a job and checking that it doesn't throw
        var jobId = registry.ScheduleJob("* * * * *", () => { });
        Assert.NotEqual(Guid.Empty, jobId);
        
        // Remove the job to trigger logger usage
        var removed = registry.RemoveJob(jobId);
        Assert.True(removed);
    }
}
