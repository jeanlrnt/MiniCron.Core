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
        Assert.True(actual >= before && actual <= after.AddMilliseconds(200));
    }
}
