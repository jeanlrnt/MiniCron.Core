using MiniCron.Core.Services;

namespace MiniCron.Tests;

public partial class MiniCronTests
{
    [Fact]
    public async Task InMemoryJobLockProvider_TTL_And_Release_Works()
    {
        var provider = new InMemoryJobLockProvider();
        var jobId = Guid.NewGuid();

        var ttl = TimeSpan.FromMilliseconds(100);

        // Acquire first time
        var a1 = await provider.TryAcquireAsync(jobId, ttl, CancellationToken.None);
        Assert.True(a1);

        // Immediate second acquire without waiting: use a short cancellation token so call returns quickly
        using var cts = new CancellationTokenSource(20);
        var a2 = await provider.TryAcquireAsync(jobId, ttl, cts.Token);
        Assert.False(a2);

        // Release should allow re-acquire
        await provider.ReleaseAsync(jobId);
        var a3 = await provider.TryAcquireAsync(jobId, ttl, CancellationToken.None);
        Assert.True(a3);

        // Acquire and wait for TTL to expire
        var jobId2 = Guid.NewGuid();
        var a4 = await provider.TryAcquireAsync(jobId2, TimeSpan.FromMilliseconds(50), CancellationToken.None);
        Assert.True(a4);
        await Task.Delay(120);
        var a5 = await provider.TryAcquireAsync(jobId2, TimeSpan.FromMilliseconds(50), CancellationToken.None);
        Assert.True(a5);
    }
}
