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

        // Immediate second acquire while lock is still held should fail immediately
        var a2 = await provider.TryAcquireAsync(jobId, ttl, CancellationToken.None);
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

    [Fact]
    public async Task InMemoryJobLockProvider_TryAcquireAsync_WithHeldLock_ReturnsFalseImmediately()
    {
        // This test validates the "Try" semantics: when a lock is held, 
        // TryAcquireAsync should return false immediately instead of blocking
        var provider = new InMemoryJobLockProvider();
        var jobId = Guid.NewGuid();
        var ttl = TimeSpan.FromSeconds(10); // Long TTL to ensure lock is held during test

        // Arrange: Acquire the lock first
        var acquired = await provider.TryAcquireAsync(jobId, ttl, CancellationToken.None);
        Assert.True(acquired, "First acquisition should succeed");

        // Act: Try to acquire the same lock again without waiting
        var secondAcquire = await provider.TryAcquireAsync(jobId, ttl, CancellationToken.None);

        // Assert: Should return false immediately
        Assert.False(secondAcquire, "Second acquisition should fail immediately");
    }

    [Fact]
    public async Task InMemoryJobLockProvider_TryAcquireAsync_DoesNotBlock_WhenLockIsHeld()
    {
        // This test verifies that TryAcquireAsync doesn't block the thread when lock is held
        var provider = new InMemoryJobLockProvider();
        var jobId = Guid.NewGuid();
        var ttl = TimeSpan.FromSeconds(10); // Long TTL

        // Acquire the lock
        await provider.TryAcquireAsync(jobId, ttl, CancellationToken.None);

        // Try to acquire multiple times in parallel - all should return false immediately
        var tasks = Enumerable.Range(0, 5)
            .Select(_ => Task.Run(async () =>
            {
                var result = await provider.TryAcquireAsync(jobId, ttl, CancellationToken.None);
                return result;
            }))
            .ToList();

        var results = await Task.WhenAll(tasks);

        // All attempts should fail
        foreach (var result in results)
        {
            Assert.False(result, "All parallel acquisitions should fail");
        }
    }

    [Fact]
    public async Task InMemoryJobLockProvider_TryAcquireAsync_WithExpiredLock_Succeeds()
    {
        // This test verifies that expired locks can be replaced immediately
        var provider = new InMemoryJobLockProvider();
        var jobId = Guid.NewGuid();
        var shortTtl = TimeSpan.FromMilliseconds(50);

        // Acquire the lock with short TTL
        var acquired = await provider.TryAcquireAsync(jobId, shortTtl, CancellationToken.None);
        Assert.True(acquired, "First acquisition should succeed");

        // Wait for the lock to expire
        await Task.Delay(TimeSpan.FromMilliseconds(100));

        // Try to acquire again - should succeed since lock expired
        var secondAcquire = await provider.TryAcquireAsync(jobId, shortTtl, CancellationToken.None);

        Assert.True(secondAcquire, "Should acquire expired lock");
    }

    [Fact]
    public async Task InMemoryJobLockProvider_TryAcquireAsync_WithCancelledToken_ReturnsFalse()
    {
        // This test verifies that the method respects cancellation tokens
        var provider = new InMemoryJobLockProvider();
        var jobId = Guid.NewGuid();
        var ttl = TimeSpan.FromSeconds(10);

        // Create a pre-cancelled token
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Try to acquire with cancelled token - should return false immediately
        var result = await provider.TryAcquireAsync(jobId, ttl, cts.Token);

        Assert.False(result, "Should return false when cancellation is requested");
    }
}
