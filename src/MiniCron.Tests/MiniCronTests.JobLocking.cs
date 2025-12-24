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
        var jobAExecuted = false;
        var jobBExecuted = false;
        var jobAStarted = new TaskCompletionSource<bool>();
        var jobBCanStart = new TaskCompletionSource<bool>();

        services.AddMiniCron(options =>
        {
            // Job A - takes time to execute
            options.AddJob("* * * * *", async (sp, ct) =>
            {
                jobAStarted.SetResult(true);
                // Wait for signal to ensure we test concurrent execution attempt
                await jobBCanStart.Task;
                await Task.Delay(10, ct); // Small delay
                jobAExecuted = true;
            });

            // Job B - quick execution with same schedule
            options.AddJob("* * * * *", async (sp, ct) =>
            {
                await Task.Delay(10, ct); // Small delay
                jobBExecuted = true;
            });
        });

        var serviceProvider = services.BuildServiceProvider();
        var registry = serviceProvider.GetRequiredService<JobRegistry>();

        // Verify we have two distinct jobs
        var jobs = registry.GetJobs();
        Assert.Equal(2, jobs.Count);
        Assert.NotSame(jobs[0], jobs[1]);

        // Allow Job B to start after Job A has started
        _ = Task.Run(async () =>
        {
            await jobAStarted.Task;
            jobBCanStart.SetResult(true);
        });

        // Act - Manually execute jobs using reflection to test the background service behavior
        var backgroundService = serviceProvider.GetServices<Microsoft.Extensions.Hosting.IHostedService>()
            .OfType<MiniCron.Core.Services.MiniCronBackgroundService>()
            .First();

        // Use reflection to access the private RunJobs method
        var runJobsMethod = typeof(MiniCron.Core.Services.MiniCronBackgroundService)
            .GetMethod("RunJobs", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        Assert.NotNull(runJobsMethod);

        // Execute RunJobs which should trigger both jobs
        var cancellationTokenSource = new CancellationTokenSource();
        var task = (Task)runJobsMethod.Invoke(backgroundService, new object[] { cancellationTokenSource.Token })!;
        await task;

        // Wait a bit for the fire-and-forget tasks to complete
        await Task.Delay(500);

        // Assert - Both jobs should have executed
        Assert.True(jobAExecuted, "Job A should have executed");
        Assert.True(jobBExecuted, "Job B should have executed even though it has the same cron expression as Job A");
    }
}
