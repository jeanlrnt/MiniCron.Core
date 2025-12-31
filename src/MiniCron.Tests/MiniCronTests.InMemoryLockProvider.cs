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
        // Use a near-immediate cancellation token timeout to avoid timing races with the TTL
        using var cts = new CancellationTokenSource(1);
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
        var delayMargin = TimeSpan.FromMilliseconds(500);
        await Task.Delay(ttlForJob2 + delayMargin, CancellationToken.None);
        var a5 = await provider.TryAcquireAsync(jobId2, ttlForJob2, CancellationToken.None);
        Assert.True(a5);
    }

    [Fact]
    public async Task InMemoryJobLockProvider_ThrowsObjectDisposedException_AfterDispose()
    {
        var provider = new InMemoryJobLockProvider();
        var jobId = Guid.NewGuid();

        // Dispose the provider
        provider.Dispose();

        // TryAcquireAsync should throw ObjectDisposedException
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await provider.TryAcquireAsync(jobId, TimeSpan.FromSeconds(1), CancellationToken.None));

        // ReleaseAsync should throw ObjectDisposedException
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await provider.ReleaseAsync(jobId));
    }
}
