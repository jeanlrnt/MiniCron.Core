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

        // Immediate second acquire while lock is still held should fail
        // Use a short cancellation token timeout (less than TTL) to ensure it fails before TTL expires
        using var cts = new CancellationTokenSource(50);
        var a2 = await provider.TryAcquireAsync(jobId, ttl, cts.Token);
        Assert.False(a2);

        // Release should allow re-acquire
        await provider.ReleaseAsync(jobId);
        await provider.ReleaseAsync(jobId);
        var a3 = await provider.TryAcquireAsync(jobId, ttl, CancellationToken.None);
        Assert.True(a3);

        // Acquire and wait for TTL to expire using a substantially larger delay than the TTL
        var jobId2 = Guid.NewGuid();
        var ttlForJob2 = TimeSpan.FromMilliseconds(50);
        var a4 = await provider.TryAcquireAsync(jobId2, ttlForJob2, CancellationToken.None);
        Assert.True(a4);
        var delayAfterTtl = ttlForJob2 + TimeSpan.FromMilliseconds(500);
        await Task.Delay(delayAfterTtl, CancellationToken.None);
        var a5 = await provider.TryAcquireAsync(jobId2, ttlForJob2, CancellationToken.None);
        Assert.True(a5);
    }
}
