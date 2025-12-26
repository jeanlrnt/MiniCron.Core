using Microsoft.Extensions.DependencyInjection;
using MiniCron.Core.Services;

namespace MiniCron.Tests;

public partial class MiniCronTests
{
    [Fact]
    public void JobRegistry_ScheduleJob_ReturnsUniqueGuid()
    {
        // Arrange
        var registry = new JobRegistry();
        var action = (IServiceProvider sp, CancellationToken ct) => Task.CompletedTask;

        // Act
        var jobId = registry.ScheduleJob("* * * * *", action);

        // Assert
        Assert.NotEqual(Guid.Empty, jobId);
        var jobs = registry.GetJobs();
        Assert.Single(jobs);
        Assert.Equal(jobId, jobs[0].Id);
    }

    [Fact]
    public void JobRegistry_ScheduleJob_MultipleJobs_ReturnsUniqueGuids()
    {
        // Arrange
        var registry = new JobRegistry();
        var action = (IServiceProvider sp, CancellationToken ct) => Task.CompletedTask;

        // Act
        var jobId1 = registry.ScheduleJob("* * * * *", action);
        var jobId2 = registry.ScheduleJob("*/5 * * * *", action);
        var jobId3 = registry.ScheduleJob("0 * * * *", action);

        // Assert
        Assert.NotEqual(jobId1, jobId2);
        Assert.NotEqual(jobId2, jobId3);
        Assert.NotEqual(jobId1, jobId3);
        Assert.Equal(3, registry.GetJobs().Count);
    }

    [Fact]
    public void JobRegistry_ScheduleJob_WithInvalidCronExpression_ThrowsArgumentException()
    {
        // Arrange
        var registry = new JobRegistry();
        var action = (IServiceProvider sp, CancellationToken ct) => Task.CompletedTask;

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            registry.ScheduleJob("invalid", action));

        Assert.Contains("must have exactly 5 fields", exception.Message);
    }

    [Fact]
    public void JobRegistry_RemoveJob_ExistingJob_ReturnsTrue()
    {
        // Arrange
        var registry = new JobRegistry();
        var action = (IServiceProvider sp, CancellationToken ct) => Task.CompletedTask;
        var jobId = registry.ScheduleJob("* * * * *", action);

        // Act
        var result = registry.RemoveJob(jobId);

        // Assert
        Assert.True(result);
        Assert.Empty(registry.GetJobs());
    }

    [Fact]
    public void JobRegistry_RemoveJob_NonExistentJob_ReturnsFalse()
    {
        // Arrange
        var registry = new JobRegistry();
        var nonExistentJobId = Guid.NewGuid();

        // Act
        var result = registry.RemoveJob(nonExistentJobId);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void JobRegistry_RemoveJob_OnlyRemovesSpecifiedJob()
    {
        // Arrange
        var registry = new JobRegistry();
        var action = (IServiceProvider sp, CancellationToken ct) => Task.CompletedTask;
        var jobId1 = registry.ScheduleJob("* * * * *", action);
        var jobId2 = registry.ScheduleJob("*/5 * * * *", action);
        var jobId3 = registry.ScheduleJob("0 * * * *", action);

        // Act
        var result = registry.RemoveJob(jobId2);

        // Assert
        Assert.True(result);
        var remainingJobs = registry.GetJobs();
        Assert.Equal(2, remainingJobs.Count);
        Assert.Contains(remainingJobs, j => j.Id == jobId1);
        Assert.Contains(remainingJobs, j => j.Id == jobId3);
        Assert.DoesNotContain(remainingJobs, j => j.Id == jobId2);
    }

    [Fact]
    public void JobRegistry_UpdateSchedule_ExistingJob_ReturnsTrue()
    {
        // Arrange
        var registry = new JobRegistry();
        var action = (IServiceProvider sp, CancellationToken ct) => Task.CompletedTask;
        var jobId = registry.ScheduleJob("* * * * *", action);

        // Act
        var result = registry.UpdateSchedule(jobId, "*/5 * * * *");

        // Assert
        Assert.True(result);
        var jobs = registry.GetJobs();
        Assert.Single(jobs);
        Assert.Equal("*/5 * * * *", jobs[0].CronExpression);
        Assert.Equal(jobId, jobs[0].Id); // ID should remain the same
    }

    [Fact]
    public void JobRegistry_UpdateSchedule_NonExistentJob_ReturnsFalse()
    {
        // Arrange
        var registry = new JobRegistry();
        var nonExistentJobId = Guid.NewGuid();

        // Act
        var result = registry.UpdateSchedule(nonExistentJobId, "*/5 * * * *");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void JobRegistry_UpdateSchedule_WithInvalidCronExpression_ThrowsArgumentException()
    {
        // Arrange
        var registry = new JobRegistry();
        var action = (IServiceProvider sp, CancellationToken ct) => Task.CompletedTask;
        var jobId = registry.ScheduleJob("* * * * *", action);

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            registry.UpdateSchedule(jobId, "invalid"));

        Assert.Contains("must have exactly 5 fields", exception.Message);
        
        // Verify job was not modified
        var jobs = registry.GetJobs();
        Assert.Single(jobs);
        Assert.Equal("* * * * *", jobs[0].CronExpression);
    }

    [Fact]
    public void JobRegistry_UpdateSchedule_OnlyUpdatesSpecifiedJob()
    {
        // Arrange
        var registry = new JobRegistry();
        var action = (IServiceProvider sp, CancellationToken ct) => Task.CompletedTask;
        var jobId1 = registry.ScheduleJob("* * * * *", action);
        var jobId2 = registry.ScheduleJob("*/5 * * * *", action);
        var jobId3 = registry.ScheduleJob("0 * * * *", action);

        // Act
        var result = registry.UpdateSchedule(jobId2, "*/10 * * * *");

        // Assert
        Assert.True(result);
        var jobs = registry.GetJobs();
        Assert.Equal(3, jobs.Count);
        
        var job1 = jobs.First(j => j.Id == jobId1);
        var job2 = jobs.First(j => j.Id == jobId2);
        var job3 = jobs.First(j => j.Id == jobId3);
        
        Assert.Equal("* * * * *", job1.CronExpression);
        Assert.Equal("*/10 * * * *", job2.CronExpression);
        Assert.Equal("0 * * * *", job3.CronExpression);
    }

    [Fact]
    public void JobRegistry_AddJob_BackwardCompatibility_StillWorks()
    {
        // Arrange
        var registry = new JobRegistry();
        var action = (IServiceProvider sp, CancellationToken ct) => Task.CompletedTask;

        // Act - Use the old AddJob method
        registry.AddJob("* * * * *", action);
        registry.AddJob("*/5 * * * *", action);

        // Assert
        Assert.Equal(2, registry.GetJobs().Count);
    }

    [Fact]
    public void JobRegistry_MixedAPI_AddJobAndScheduleJob_WorkTogether()
    {
        // Arrange
        var registry = new JobRegistry();
        var action = (IServiceProvider sp, CancellationToken ct) => Task.CompletedTask;

        // Act - Mix old and new APIs
        registry.AddJob("* * * * *", action);
        var jobId = registry.ScheduleJob("*/5 * * * *", action);
        registry.AddJob("0 * * * *", action);

        // Assert
        Assert.Equal(3, registry.GetJobs().Count);
        
        // Can remove the scheduled job by ID
        var removed = registry.RemoveJob(jobId);
        Assert.True(removed);
        Assert.Equal(2, registry.GetJobs().Count);
    }

    [Fact]
    public async Task JobRegistry_UpdateSchedule_PreservesJobAction()
    {
        // Arrange
        var registry = new JobRegistry();
        var executionCounter = 0;
        var action = (IServiceProvider sp, CancellationToken ct) =>
        {
            executionCounter++;
            return Task.CompletedTask;
        };
        
        var jobId = registry.ScheduleJob("* * * * *", action);

        // Act - Update the schedule
        registry.UpdateSchedule(jobId, "*/5 * * * *");

        // Assert - The action should still be the same
        var jobs = registry.GetJobs();
        var updatedJob = jobs.First(j => j.Id == jobId);
        
        // Execute the action to verify it's still the same
        await updatedJob.Action(null!, CancellationToken.None);
        Assert.Equal(1, executionCounter);
    }

    [Fact]
    public void JobRegistry_ConcurrentScheduleJob_AllJobsAdded()
    {
        // Arrange
        var registry = new JobRegistry();
        var action = (IServiceProvider sp, CancellationToken ct) => Task.CompletedTask;
        var jobIds = new System.Collections.Concurrent.ConcurrentBag<Guid>();

        // Act - Schedule jobs concurrently from multiple threads
        Parallel.For(0, 10, _ =>
        {
            var jobId = registry.ScheduleJob("* * * * *", action);
            jobIds.Add(jobId);
        });

        // Assert
        Assert.Equal(10, jobIds.Count);
        Assert.Equal(10, jobIds.Distinct().Count()); // All IDs should be unique
        Assert.Equal(10, registry.GetJobs().Count);
    }

    [Fact]
    public void JobRegistry_ConcurrentRemoveJob_ThreadSafe()
    {
        // Arrange
        var registry = new JobRegistry();
        var action = (IServiceProvider sp, CancellationToken ct) => Task.CompletedTask;
        var jobIds = new List<Guid>();
        
        for (int i = 0; i < 10; i++)
        {
            jobIds.Add(registry.ScheduleJob("* * * * *", action));
        }

        // Act - Remove jobs concurrently from multiple threads
        var removeResults = new System.Collections.Concurrent.ConcurrentBag<bool>();
        Parallel.ForEach(jobIds, jobId =>
        {
            var result = registry.RemoveJob(jobId);
            removeResults.Add(result);
        });

        // Assert
        Assert.Equal(10, removeResults.Count);
        Assert.All(removeResults, result => Assert.True(result)); // All removes should succeed
        Assert.Empty(registry.GetJobs());
    }

    [Fact]
    public void JobRegistry_ConcurrentUpdateSchedule_ThreadSafe()
    {
        // Arrange
        var registry = new JobRegistry();
        var action = (IServiceProvider sp, CancellationToken ct) => Task.CompletedTask;
        var jobIds = new List<Guid>();
        
        for (int i = 0; i < 10; i++)
        {
            jobIds.Add(registry.ScheduleJob("* * * * *", action));
        }

        // Act - Update schedules concurrently from multiple threads
        var updateResults = new System.Collections.Concurrent.ConcurrentBag<bool>();
        Parallel.ForEach(jobIds, jobId =>
        {
            var result = registry.UpdateSchedule(jobId, "*/5 * * * *");
            updateResults.Add(result);
        });

        // Assert
        Assert.Equal(10, updateResults.Count);
        Assert.All(updateResults, result => Assert.True(result)); // All updates should succeed
        var jobs = registry.GetJobs();
        Assert.Equal(10, jobs.Count);
        Assert.All(jobs, job => Assert.Equal("*/5 * * * *", job.CronExpression));
    }

    [Fact]
    public void JobRegistry_ConcurrentMixedOperations_ThreadSafe()
    {
        // Arrange
        var registry = new JobRegistry();
        var action = (IServiceProvider sp, CancellationToken ct) => Task.CompletedTask;
        
        // Pre-populate with some jobs
        var existingJobIds = new List<Guid>();
        for (int i = 0; i < 5; i++)
        {
            existingJobIds.Add(registry.ScheduleJob("* * * * *", action));
        }

        // Act - Mix of operations happening concurrently
        var results = new System.Collections.Concurrent.ConcurrentBag<string>();
        Parallel.Invoke(
            // Thread 1: Schedule new jobs
            () =>
            {
                for (int i = 0; i < 5; i++)
                {
                    registry.ScheduleJob("*/2 * * * *", action);
                    results.Add("scheduled");
                }
            },
            // Thread 2: Update existing jobs
            () =>
            {
                foreach (var jobId in existingJobIds.Take(3))
                {
                    registry.UpdateSchedule(jobId, "*/3 * * * *");
                    results.Add("updated");
                }
            },
            // Thread 3: Remove some jobs
            () =>
            {
                foreach (var jobId in existingJobIds.Skip(3))
                {
                    registry.RemoveJob(jobId);
                    results.Add("removed");
                }
            },
            // Thread 4: Read jobs
            () =>
            {
                for (int i = 0; i < 10; i++)
                {
                    var jobs = registry.GetJobs();
                    results.Add($"read-{jobs.Count}");
                }
            }
        );

        // Assert - Should not throw and operations should be successful
        Assert.NotEmpty(results);
        var finalJobs = registry.GetJobs();
        Assert.InRange(finalJobs.Count, 1, 10); // Should have some jobs remaining
    }

    [Fact]
    public async Task BackgroundService_UpdateSchedule_WhileJobRunning_UsesNewSchedule()
    {
        // Arrange
        var services = new ServiceCollection();
        var jobStarted = new TaskCompletionSource<bool>();
        var jobCanComplete = new TaskCompletionSource<bool>();
        var executionCount = 0;

        var registry = new JobRegistry();
        var jobId = registry.ScheduleJob("* * * * *", async (sp, ct) =>
        {
            Interlocked.Increment(ref executionCount);
            jobStarted.SetResult(true);
            await jobCanComplete.Task;
        });

        services.AddSingleton(registry);
        services.AddSingleton<Microsoft.Extensions.Hosting.IHostedService, MiniCronBackgroundService>();
        services.AddLogging();

        var serviceProvider = services.BuildServiceProvider();
        var backgroundService = serviceProvider.GetServices<Microsoft.Extensions.Hosting.IHostedService>()
            .OfType<MiniCronBackgroundService>()
            .First();

        var runJobsMethod = typeof(MiniCronBackgroundService)
            .GetMethod("RunJobs", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        Assert.NotNull(runJobsMethod);

        using var cts = new CancellationTokenSource();

        // Act - Start job execution
        var task = (Task)runJobsMethod.Invoke(backgroundService, new object[] { cts.Token })!;
        await task;
        await jobStarted.Task; // Wait for job to start

        // Update the schedule while job is running
        var updateResult = registry.UpdateSchedule(jobId, "*/5 * * * *");
        Assert.True(updateResult);

        // Verify the job in registry has the new schedule
        var updatedJobs = registry.GetJobs();
        var updatedJob = updatedJobs.First(j => j.Id == jobId);
        Assert.Equal("*/5 * * * *", updatedJob.CronExpression);

        // Complete the running job
        jobCanComplete.SetResult(true);
        await Task.Delay(100); // Give time for cleanup

        // Assert - The job should have executed once and the schedule is updated
        Assert.Equal(1, executionCount);
    }

    [Fact]
    public async Task BackgroundService_RemoveJob_WhileJobRunning_AllowsCurrentExecutionToComplete()
    {
        // Arrange
        var services = new ServiceCollection();
        var jobStarted = new TaskCompletionSource<bool>();
        var jobCanComplete = new TaskCompletionSource<bool>();
        var jobCompleted = new TaskCompletionSource<bool>();

        var registry = new JobRegistry();
        var jobId = registry.ScheduleJob("* * * * *", async (sp, ct) =>
        {
            jobStarted.SetResult(true);
            await jobCanComplete.Task;
            jobCompleted.SetResult(true);
        });

        services.AddSingleton(registry);
        services.AddSingleton<Microsoft.Extensions.Hosting.IHostedService, MiniCronBackgroundService>();
        services.AddLogging();

        var serviceProvider = services.BuildServiceProvider();
        var backgroundService = serviceProvider.GetServices<Microsoft.Extensions.Hosting.IHostedService>()
            .OfType<MiniCronBackgroundService>()
            .First();

        var runJobsMethod = typeof(MiniCronBackgroundService)
            .GetMethod("RunJobs", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        Assert.NotNull(runJobsMethod);

        using var cts = new CancellationTokenSource();

        // Act - Start job execution
        var task = (Task)runJobsMethod.Invoke(backgroundService, new object[] { cts.Token })!;
        await task;
        await jobStarted.Task; // Wait for job to start

        // Remove the job while it's running
        var removeResult = registry.RemoveJob(jobId);
        Assert.True(removeResult);

        // Verify the job is no longer in registry
        Assert.Empty(registry.GetJobs());

        // Allow the job to complete
        jobCanComplete.SetResult(true);
        
        // Wait for job to finish
        var completedInTime = await Task.WhenAny(jobCompleted.Task, Task.Delay(2000)) == jobCompleted.Task;

        // Assert - The job should have completed its current execution
        Assert.True(completedInTime, "Job should have completed its execution even after being removed");
        Assert.True(jobCompleted.Task.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task BackgroundService_UpdateSchedule_PreventsDuplicateExecutionWithNewSchedule()
    {
        // Arrange - This test verifies that tracking by Guid prevents the duplicate execution bug
        var services = new ServiceCollection();
        var executionCount = 0;

        var registry = new JobRegistry();
        var jobId = registry.ScheduleJob("* * * * *", async (sp, ct) =>
        {
            Interlocked.Increment(ref executionCount);
            await Task.Delay(50, ct);
        });

        services.AddSingleton(registry);
        services.AddSingleton<Microsoft.Extensions.Hosting.IHostedService, MiniCronBackgroundService>();
        services.AddLogging();

        var serviceProvider = services.BuildServiceProvider();
        var backgroundService = serviceProvider.GetServices<Microsoft.Extensions.Hosting.IHostedService>()
            .OfType<MiniCronBackgroundService>()
            .First();

        var runJobsMethod = typeof(MiniCronBackgroundService)
            .GetMethod("RunJobs", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        Assert.NotNull(runJobsMethod);

        using var cts = new CancellationTokenSource();

        // Act - Run the job, update schedule, then run again immediately
        var task1 = (Task)runJobsMethod.Invoke(backgroundService, new object[] { cts.Token })!;
        await task1;

        // Update the schedule (creates a new CronJob instance with different CronExpression)
        registry.UpdateSchedule(jobId, "*/5 * * * *");

        // Try to run again immediately (job is still running from first call)
        var task2 = (Task)runJobsMethod.Invoke(backgroundService, new object[] { cts.Token })!;
        await task2;

        await Task.Delay(200); // Wait for jobs to complete

        // Assert - Should have executed only once despite schedule update
        // This verifies that tracking by Guid (not CronJob instance) prevents duplicate execution
        Assert.Equal(1, executionCount);
    }
}
