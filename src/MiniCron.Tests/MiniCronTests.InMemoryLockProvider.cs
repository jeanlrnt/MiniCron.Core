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

    [Fact]
    public async Task InMemoryJobLockProvider_MultipleJobIds_AreIndependent()
    {
        // Test that locks for different job IDs are isolated from each other
        var provider = new InMemoryJobLockProvider();
        var jobId1 = Guid.NewGuid();
        var jobId2 = Guid.NewGuid();
        var jobId3 = Guid.NewGuid();
        var ttl = TimeSpan.FromSeconds(10);

        // Acquire locks for all three jobs
        var acquired1 = await provider.TryAcquireAsync(jobId1, ttl, CancellationToken.None);
        var acquired2 = await provider.TryAcquireAsync(jobId2, ttl, CancellationToken.None);
        var acquired3 = await provider.TryAcquireAsync(jobId3, ttl, CancellationToken.None);

        // All should succeed since they're different job IDs
        Assert.True(acquired1, "Job 1 lock should be acquired");
        Assert.True(acquired2, "Job 2 lock should be acquired");
        Assert.True(acquired3, "Job 3 lock should be acquired");

        // Attempting to acquire any of them again should fail
        Assert.False(await provider.TryAcquireAsync(jobId1, ttl, CancellationToken.None));
        Assert.False(await provider.TryAcquireAsync(jobId2, ttl, CancellationToken.None));
        Assert.False(await provider.TryAcquireAsync(jobId3, ttl, CancellationToken.None));

        // Release one lock - only that job should be re-acquirable
        await provider.ReleaseAsync(jobId2);
        Assert.False(await provider.TryAcquireAsync(jobId1, ttl, CancellationToken.None));
        Assert.True(await provider.TryAcquireAsync(jobId2, ttl, CancellationToken.None));
        Assert.False(await provider.TryAcquireAsync(jobId3, ttl, CancellationToken.None));
    }

    [Fact]
    public async Task InMemoryJobLockProvider_ConcurrentAcquireAndRelease_ThreadSafe()
    {
        // Test thread safety when multiple threads try to acquire and release locks
        var provider = new InMemoryJobLockProvider();
        var jobId = Guid.NewGuid();
        var ttl = TimeSpan.FromSeconds(1);
        var successCount = 0;

        // Run 20 concurrent tasks that try to acquire and release
        var tasks = Enumerable.Range(0, 20).Select(_ => Task.Run(async () =>
        {
            for (int i = 0; i < 10; i++)
            {
                if (await provider.TryAcquireAsync(jobId, ttl, CancellationToken.None))
                {
                    Interlocked.Increment(ref successCount);
                    await Task.Delay(10); // Hold the lock briefly
                    await provider.ReleaseAsync(jobId);
                }
                await Task.Delay(5); // Small delay between attempts
            }
        })).ToArray();

        await Task.WhenAll(tasks);

        // At least some attempts should have succeeded
        Assert.True(successCount > 0, $"Expected some lock acquisitions to succeed, but got {successCount}");
    }

    [Fact]
    public async Task InMemoryJobLockProvider_RaceConditionOnExpiredLock_OnlyOneAcquires()
    {
        // Test the race condition where multiple threads try to acquire an expired lock simultaneously
        var provider = new InMemoryJobLockProvider();
        var jobId = Guid.NewGuid();
        var shortTtl = TimeSpan.FromMilliseconds(50);

        // First acquire the lock
        var initialAcquire = await provider.TryAcquireAsync(jobId, shortTtl, CancellationToken.None);
        Assert.True(initialAcquire, "Initial acquisition should succeed");

        // Wait for lock to expire
        await Task.Delay(TimeSpan.FromMilliseconds(100));

        // Launch multiple threads to try to acquire the expired lock simultaneously
        var tasks = Enumerable.Range(0, 10).Select(_ => Task.Run(async () =>
        {
            return await provider.TryAcquireAsync(jobId, TimeSpan.FromSeconds(1), CancellationToken.None);
        })).ToArray();

        var results = await Task.WhenAll(tasks);

        // Only one thread should successfully acquire the lock (thanks to TryUpdate compare-and-swap)
        var successCount = results.Count(r => r);
        Assert.Equal(1, successCount);
    }

    [Fact]
    public async Task InMemoryJobLockProvider_ReleaseNonExistentLock_DoesNotThrow()
    {
        // Test that releasing a lock that was never acquired doesn't cause issues
        var provider = new InMemoryJobLockProvider();
        var jobId = Guid.NewGuid();

        // Should not throw
        await provider.ReleaseAsync(jobId);
        
        // Should still be able to acquire afterwards
        var acquired = await provider.TryAcquireAsync(jobId, TimeSpan.FromSeconds(1), CancellationToken.None);
        Assert.True(acquired, "Should be able to acquire after releasing non-existent lock");
    }

    [Fact]
    public async Task InMemoryJobLockProvider_RepeatedAcquireReleasecycles_WorksCorrectly()
    {
        // Test repeated acquire/release cycles on the same job ID
        var provider = new InMemoryJobLockProvider();
        var jobId = Guid.NewGuid();
        var ttl = TimeSpan.FromMilliseconds(100);

        for (int i = 0; i < 10; i++)
        {
            // Acquire
            var acquired = await provider.TryAcquireAsync(jobId, ttl, CancellationToken.None);
            Assert.True(acquired, $"Acquisition {i + 1} should succeed");

            // Verify it's held
            var secondAcquire = await provider.TryAcquireAsync(jobId, ttl, CancellationToken.None);
            Assert.False(secondAcquire, $"Second acquisition {i + 1} should fail");

            // Release
            await provider.ReleaseAsync(jobId);
        }
    }

    [Fact]
    public void InMemoryJobLockProvider_DisposeIsIdempotent()
    {
        // Test that calling Dispose multiple times is safe
        var provider = new InMemoryJobLockProvider();

        // First dispose
        provider.Dispose();

        // Second dispose should not throw
        provider.Dispose();

        // Third dispose should not throw
        provider.Dispose();
    }

    [Fact]
    public async Task InMemoryJobLockProvider_VeryShortTTL_WorksCorrectly()
    {
        // Test with a very short TTL to ensure edge case handling
        var provider = new InMemoryJobLockProvider();
        var jobId = Guid.NewGuid();
        var veryShortTtl = TimeSpan.FromMilliseconds(1);

        var acquired = await provider.TryAcquireAsync(jobId, veryShortTtl, CancellationToken.None);
        Assert.True(acquired, "Should acquire with very short TTL");

        // Wait for expiry
        await Task.Delay(TimeSpan.FromMilliseconds(10));

        // Should be able to acquire again
        var secondAcquire = await provider.TryAcquireAsync(jobId, veryShortTtl, CancellationToken.None);
        Assert.True(secondAcquire, "Should re-acquire after very short TTL expires");
    }

    [Fact]
    public async Task InMemoryJobLockProvider_LongTTL_WorksCorrectly()
    {
        // Test with a very long TTL
        var provider = new InMemoryJobLockProvider();
        var jobId = Guid.NewGuid();
        var longTtl = TimeSpan.FromHours(1);

        var acquired = await provider.TryAcquireAsync(jobId, longTtl, CancellationToken.None);
        Assert.True(acquired, "Should acquire with long TTL");

        // Verify it's held
        var secondAcquire = await provider.TryAcquireAsync(jobId, longTtl, CancellationToken.None);
        Assert.False(secondAcquire, "Should not re-acquire while long TTL is active");
    }

    [Fact]
    public async Task InMemoryJobLockProvider_ZeroTTL_ExpiresImmediately()
    {
        // Test with zero TTL - lock should expire immediately
        var provider = new InMemoryJobLockProvider();
        var jobId = Guid.NewGuid();
        var zeroTtl = TimeSpan.Zero;

        var acquired = await provider.TryAcquireAsync(jobId, zeroTtl, CancellationToken.None);
        Assert.True(acquired, "Should acquire with zero TTL");

        // Should be able to acquire immediately since TTL is zero (already expired)
        var secondAcquire = await provider.TryAcquireAsync(jobId, TimeSpan.FromSeconds(1), CancellationToken.None);
        Assert.True(secondAcquire, "Should re-acquire immediately with zero TTL");
    }

    [Fact]
    public async Task InMemoryJobLockProvider_ConcurrentExpiredLockReplace_HandledCorrectly()
    {
        // Test scenario where lock expires and multiple threads race to replace it
        var provider = new InMemoryJobLockProvider();
        var jobId = Guid.NewGuid();
        var shortTtl = TimeSpan.FromMilliseconds(50);

        // Acquire initial lock
        await provider.TryAcquireAsync(jobId, shortTtl, CancellationToken.None);

        // Wait for it to expire
        await Task.Delay(TimeSpan.FromMilliseconds(100));

        // Multiple threads try to acquire the expired lock
        var concurrentAttempts = 50;
        var tasks = Enumerable.Range(0, concurrentAttempts).Select(_ => Task.Run(async () =>
        {
            return await provider.TryAcquireAsync(jobId, TimeSpan.FromSeconds(1), CancellationToken.None);
        })).ToArray();

        var results = await Task.WhenAll(tasks);

        // Exactly one should succeed
        var successCount = results.Count(r => r);
        Assert.Equal(1, successCount);
    }

    [Fact]
    public async Task InMemoryJobLockProvider_AcquireAfterRelease_MultipleJobIds()
    {
        // Test that after releasing, a different job can acquire
        var provider = new InMemoryJobLockProvider();
        var jobId1 = Guid.NewGuid();
        var jobId2 = Guid.NewGuid();
        var ttl = TimeSpan.FromSeconds(10);

        // Acquire first job
        var acquired1 = await provider.TryAcquireAsync(jobId1, ttl, CancellationToken.None);
        Assert.True(acquired1);

        // Second job can still acquire (different ID)
        var acquired2 = await provider.TryAcquireAsync(jobId2, ttl, CancellationToken.None);
        Assert.True(acquired2);

        // Release first job
        await provider.ReleaseAsync(jobId1);

        // First job can be re-acquired
        var reacquired1 = await provider.TryAcquireAsync(jobId1, ttl, CancellationToken.None);
        Assert.True(reacquired1);

        // Second job still held
        var tryAcquire2Again = await provider.TryAcquireAsync(jobId2, ttl, CancellationToken.None);
        Assert.False(tryAcquire2Again);
    }
}
