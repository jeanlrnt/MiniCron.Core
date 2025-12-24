using Microsoft.Extensions.DependencyInjection;
using MiniCron.Core.Extensions;
using MiniCron.Core.Services;

namespace MiniCron.Tests;

public partial class MiniCronTests
{
    [Fact]
    public void JobRegistry_AddMultipleJobsWithSameCronExpression_CreatesDistinctJobs()
    {
        // Arrange
        var registry = new JobRegistry();
        var action1 = (IServiceProvider sp, CancellationToken ct) => Task.CompletedTask;
        var action2 = (IServiceProvider sp, CancellationToken ct) => Task.CompletedTask;
        var sameCronExpression = "* * * * *";

        // Act
        registry.AddJob(sameCronExpression, action1);
        registry.AddJob(sameCronExpression, action2);

        // Assert - Should have two distinct jobs even with same cron expression
        var jobs = registry.GetJobs();
        Assert.Equal(2, jobs.Count);
        Assert.Equal(sameCronExpression, jobs[0].CronExpression);
        Assert.Equal(sameCronExpression, jobs[1].CronExpression);
        
        // Verify the jobs are distinct instances (reference equality)
        Assert.NotSame(jobs[0], jobs[1]);
    }

    [Fact]
    public async Task BackgroundService_MultipleJobsWithSameCronExpression_BothExecuteIndependently()
    {
        // Arrange
        var services = new ServiceCollection();
        var jobAExecuted = new TaskCompletionSource<bool>();
        var jobBExecuted = new TaskCompletionSource<bool>();

        services.AddMiniCron(options =>
        {
            // Job A - marks completion
            options.AddJob("* * * * *", async (sp, ct) =>
            {
                await Task.Delay(50, ct); // Simulate work
                jobAExecuted.SetResult(true);
            });

            // Job B - marks completion with same schedule
            options.AddJob("* * * * *", async (sp, ct) =>
            {
                await Task.Delay(50, ct); // Simulate work
                jobBExecuted.SetResult(true);
            });
        });

        var serviceProvider = services.BuildServiceProvider();
        var registry = serviceProvider.GetRequiredService<JobRegistry>();

        // Verify we have two distinct jobs
        var jobs = registry.GetJobs();
        Assert.Equal(2, jobs.Count);
        Assert.NotSame(jobs[0], jobs[1]);

        // Act - Manually execute jobs using reflection to test the background service behavior
        var backgroundService = serviceProvider.GetServices<Microsoft.Extensions.Hosting.IHostedService>()
            .OfType<MiniCron.Core.Services.MiniCronBackgroundService>()
            .First();

        // Use reflection to access the private RunJobs method
        var runJobsMethod = typeof(MiniCron.Core.Services.MiniCronBackgroundService)
            .GetMethod("RunJobs", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        Assert.NotNull(runJobsMethod);

        // Execute RunJobs which should trigger both jobs
        using var cancellationTokenSource = new CancellationTokenSource();
        var task = (Task)runJobsMethod.Invoke(backgroundService, new object[] { cancellationTokenSource.Token })!;
        await task;

        // Wait for both jobs to complete using TaskCompletionSource
        var completionTask = Task.WhenAll(jobAExecuted.Task, jobBExecuted.Task);
        var completedInTime = await Task.WhenAny(completionTask, Task.Delay(2000)) == completionTask;

        // Assert - Both jobs should have executed
        Assert.True(completedInTime, "Both jobs should have completed within the timeout");
        Assert.True(jobAExecuted.Task.IsCompletedSuccessfully, "Job A should have executed");
        Assert.True(jobBExecuted.Task.IsCompletedSuccessfully, "Job B should have executed even though it has the same cron expression as Job A");
    }

    [Fact]
    public void JobRegistry_AddMultipleJobsWithSameDelegateInstance_CreatesDistinctJobs()
    {
        // Arrange - Test edge case where same delegate instance is reused
        var registry = new JobRegistry();
        var sharedAction = (IServiceProvider sp, CancellationToken ct) => Task.CompletedTask;
        var sameCronExpression = "* * * * *";

        // Act
        registry.AddJob(sameCronExpression, sharedAction);
        registry.AddJob(sameCronExpression, sharedAction);

        // Assert - Should have two distinct jobs even with same delegate and cron expression
        var jobs = registry.GetJobs();
        Assert.Equal(2, jobs.Count);
        Assert.Equal(sameCronExpression, jobs[0].CronExpression);
        Assert.Equal(sameCronExpression, jobs[1].CronExpression);
        
        // Verify the jobs are distinct instances despite sharing delegate
        Assert.NotSame(jobs[0], jobs[1]);
        // Verify unique IDs
        Assert.NotEqual(jobs[0].Id, jobs[1].Id);
    }

    [Fact]
    public async Task BackgroundService_SameJobInstance_CannotRunConcurrently()
    {
        // Arrange - Test that the same job instance is still protected from concurrent execution
        var services = new ServiceCollection();
        var jobStarted = new TaskCompletionSource<bool>();
        var jobCanFinish = new TaskCompletionSource<bool>();
        var executionCount = 0;

        services.AddMiniCron(options =>
        {
            options.AddJob("* * * * *", async (sp, ct) =>
            {
                Interlocked.Increment(ref executionCount);
                jobStarted.SetResult(true);
                await jobCanFinish.Task;
            });
        });

        var serviceProvider = services.BuildServiceProvider();
        var backgroundService = serviceProvider.GetServices<Microsoft.Extensions.Hosting.IHostedService>()
            .OfType<MiniCron.Core.Services.MiniCronBackgroundService>()
            .First();

        var runJobsMethod = typeof(MiniCron.Core.Services.MiniCronBackgroundService)
            .GetMethod("RunJobs", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        Assert.NotNull(runJobsMethod);

        // Act - Try to run the same job twice concurrently
        using var cancellationTokenSource = new CancellationTokenSource();
        
        // First execution
        var task1 = (Task)runJobsMethod.Invoke(backgroundService, new object[] { cancellationTokenSource.Token })!;
        await task1;
        await jobStarted.Task; // Wait for first execution to start

        // Second execution attempt (should be skipped due to lock)
        var task2 = (Task)runJobsMethod.Invoke(backgroundService, new object[] { cancellationTokenSource.Token })!;
        await task2;
        
        // Allow first execution to complete
        jobCanFinish.SetResult(true);
        await Task.Delay(100); // Give time for cleanup

        // Assert - Should have only executed once (second was skipped)
        Assert.Equal(1, executionCount);
    }

    [Fact]
    public async Task BackgroundService_DifferentJobsSameCronAndDelegate_BothExecuteIndependently()
    {
        // Arrange - Test that even with same delegate instance, different jobs execute independently
        var services = new ServiceCollection();
        var job1Executed = new TaskCompletionSource<bool>();
        var job2Executed = new TaskCompletionSource<bool>();
        var sharedAction = async (IServiceProvider sp, CancellationToken ct) =>
        {
            // Both jobs use this same delegate, but they should still be tracked separately
            await Task.Delay(10, ct);
        };

        // Manually create the registry to control job creation
        var registry = new JobRegistry();
        var cronExpression = "* * * * *";
        
        // Create first job wrapper that marks completion
        registry.AddJob(cronExpression, async (sp, ct) =>
        {
            await sharedAction(sp, ct);
            job1Executed.SetResult(true);
        });
        
        // Create second job wrapper that marks completion
        registry.AddJob(cronExpression, async (sp, ct) =>
        {
            await sharedAction(sp, ct);
            job2Executed.SetResult(true);
        });

        services.AddSingleton(registry);
        services.AddSingleton<Microsoft.Extensions.Hosting.IHostedService, MiniCron.Core.Services.MiniCronBackgroundService>();
        services.AddLogging();

        var serviceProvider = services.BuildServiceProvider();
        var backgroundService = serviceProvider.GetServices<Microsoft.Extensions.Hosting.IHostedService>()
            .OfType<MiniCron.Core.Services.MiniCronBackgroundService>()
            .First();

        var runJobsMethod = typeof(MiniCron.Core.Services.MiniCronBackgroundService)
            .GetMethod("RunJobs", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        Assert.NotNull(runJobsMethod);

        // Act
        using var cancellationTokenSource = new CancellationTokenSource();
        var task = (Task)runJobsMethod.Invoke(backgroundService, new object[] { cancellationTokenSource.Token })!;
        await task;

        // Wait for both jobs to complete
        var completionTask = Task.WhenAll(job1Executed.Task, job2Executed.Task);
        var completedInTime = await Task.WhenAny(completionTask, Task.Delay(2000)) == completionTask;

        // Assert - Both jobs should have executed independently
        Assert.True(completedInTime, "Both jobs should have completed within the timeout");
        Assert.True(job1Executed.Task.IsCompletedSuccessfully, "Job 1 should have executed");
        Assert.True(job2Executed.Task.IsCompletedSuccessfully, "Job 2 should have executed");
    }
}
