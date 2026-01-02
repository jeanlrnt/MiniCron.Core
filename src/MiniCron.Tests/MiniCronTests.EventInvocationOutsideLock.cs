using MiniCron.Core.Services;

namespace MiniCron.Tests;

public partial class MiniCronTests
{
    /// <summary>
    /// Verifies that JobAdded event is invoked outside the write lock,
    /// allowing concurrent GetJobs() calls to proceed without blocking.
    /// </summary>
    [Fact]
    public async Task JobRegistry_JobAdded_InvokedOutsideLock_AllowsConcurrentReads()
    {
        var registry = new JobRegistry();
        var eventHandlerStarted = new TaskCompletionSource<bool>();
        var eventHandlerCanComplete = new ManualResetEventSlim(false);
        var getJobsCompleted = new TaskCompletionSource<bool>();

        // Subscribe to JobAdded with a long-running handler
        registry.JobAdded += (_, _) =>
        {
            eventHandlerStarted.SetResult(true);
            // Simulate a slow event handler (e.g., logging to database)
            // Block synchronously until signaled
            eventHandlerCanComplete.Wait();
        };

        // Schedule a job (which will trigger JobAdded event)
        var scheduleTask = Task.Run(() =>
        {
            registry.ScheduleJob("* * * * *", () => { });
        });

        // Wait for event handler to start
        await eventHandlerStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // While event handler is running, try to read jobs
        // This should NOT block because the event is invoked outside the lock
        var getJobsTask = Task.Run(() =>
        {
            var jobs = registry.GetJobs();
            Assert.Single(jobs); // Job should be in registry
            getJobsCompleted.SetResult(true);
        });

        // Wait for GetJobs to complete (should happen quickly)
        await Task.WhenAll(getJobsCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5)), getJobsTask);

        // Now allow event handler to complete
        eventHandlerCanComplete.Set();
        await scheduleTask;
    }

    /// <summary>
    /// Verifies that JobRemoved event is invoked outside the write lock.
    /// </summary>
    [Fact]
    public async Task JobRegistry_JobRemoved_InvokedOutsideLock_AllowsConcurrentReads()
    {
        var registry = new JobRegistry();
        var jobId = registry.ScheduleJob("* * * * *", () => { });
        
        var eventHandlerStarted = new TaskCompletionSource<bool>();
        var eventHandlerCanComplete = new ManualResetEventSlim(false);
        var getJobsCompleted = new TaskCompletionSource<bool>();

        // Subscribe to JobRemoved with a long-running handler
        registry.JobRemoved += (_, _) =>
        {
            eventHandlerStarted.SetResult(true);
            eventHandlerCanComplete.Wait();
        };

        // Remove the job (which will trigger JobRemoved event)
        var removeTask = Task.Run(() =>
        {
            registry.RemoveJob(jobId);
        });

        // Wait for event handler to start
        await eventHandlerStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // While event handler is running, try to read jobs
        var getJobsTask = Task.Run(() =>
        {
            var jobs = registry.GetJobs();
            Assert.Empty(jobs); // Job should be removed from registry
            getJobsCompleted.SetResult(true);
        });

        // Wait for GetJobs to complete
        await Task.WhenAll(getJobsCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5)), getJobsTask);

        // Allow event handler to complete
        eventHandlerCanComplete.Set();
        await removeTask;
    }

    /// <summary>
    /// Verifies that JobUpdated event is invoked outside the write lock.
    /// </summary>
    [Fact]
    public async Task JobRegistry_JobUpdated_InvokedOutsideLock_AllowsConcurrentReads()
    {
        var registry = new JobRegistry();
        var jobId = registry.ScheduleJob("* * * * *", () => { });
        
        var eventHandlerStarted = new TaskCompletionSource<bool>();
        var eventHandlerCanComplete = new ManualResetEventSlim(false);
        var getJobsCompleted = new TaskCompletionSource<bool>();

        // Subscribe to JobUpdated with a long-running handler
        registry.JobUpdated += (_, _) =>
        {
            eventHandlerStarted.SetResult(true);
            eventHandlerCanComplete.Wait();
        };

        // Update the job schedule (which will trigger JobUpdated event)
        var updateTask = Task.Run(() =>
        {
            registry.UpdateSchedule(jobId, "*/5 * * * *");
        });

        // Wait for event handler to start
        await eventHandlerStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // While event handler is running, try to read jobs
        var getJobsTask = Task.Run(() =>
        {
            var jobs = registry.GetJobs();
            Assert.Single(jobs);
            Assert.Equal("*/5 * * * *", jobs[0].CronExpression); // Should see updated schedule
            getJobsCompleted.SetResult(true);
        });

        // Wait for GetJobs to complete
        await Task.WhenAll(getJobsCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5)), getJobsTask);

        // Allow event handler to complete
        eventHandlerCanComplete.Set();
        await updateTask;
    }

    /// <summary>
    /// Verifies that exceptions in event handlers are caught and logged,
    /// and do not prevent the operation from completing.
    /// </summary>
    [Fact]
    public void JobRegistry_EventHandler_Exception_DoesNotPreventOperation()
    {
        var registry = new JobRegistry();
        var exceptionThrown = false;

        // Subscribe with a handler that throws
        registry.JobAdded += (_, _) =>
        {
            exceptionThrown = true;
            throw new InvalidOperationException("Test exception in event handler");
        };

        // Schedule a job - should complete successfully despite exception
        var jobId = registry.ScheduleJob("* * * * *", () => { });
        
        Assert.True(exceptionThrown);
        Assert.NotEqual(Guid.Empty, jobId);
        
        // Job should still be in registry
        var jobs = registry.GetJobs();
        Assert.Single(jobs);
        Assert.Equal(jobId, jobs[0].Id);
    }

    /// <summary>
    /// Verifies that event handlers can safely call back into JobRegistry
    /// without deadlock, since events are invoked outside the lock.
    /// </summary>
    [Fact]
    public void JobRegistry_EventHandler_CanCallBackIntoRegistry_NoDeadlock()
    {
        var registry = new JobRegistry();
        Guid? secondJobId = null;

        // Subscribe with a handler that schedules another job
        registry.JobAdded += (sender, args) =>
        {
            var reg = (JobRegistry)sender!;
            // This should not deadlock because event is invoked outside the lock
            if (args.Job.CronExpression == "* * * * *")
            {
                secondJobId = reg.ScheduleJob("*/5 * * * *", () => { });
            }
        };

        // Schedule first job - should trigger handler that schedules second job
        var firstJobId = registry.ScheduleJob("* * * * *", () => { });
        
        Assert.NotNull(secondJobId);
        Assert.NotEqual(firstJobId, secondJobId);
        
        // Both jobs should be in registry
        var jobs = registry.GetJobs();
        Assert.Equal(2, jobs.Count);
    }

    /// <summary>
    /// Verifies that JobAdded event receives correct job information.
    /// </summary>
    [Fact]
    public void JobRegistry_JobAdded_EventArgs_ContainCorrectJobInfo()
    {
        var registry = new JobRegistry();
        JobEventArgs? capturedArgs = null;

        registry.JobAdded += (_, args) =>
        {
            capturedArgs = args;
        };

        var jobId = registry.ScheduleJob("*/10 * * * *", () => { });

        Assert.NotNull(capturedArgs);
        Assert.Equal(jobId, capturedArgs.Job.Id);
        Assert.Equal("*/10 * * * *", capturedArgs.Job.CronExpression);
        Assert.Null(capturedArgs.PreviousJob);
    }

    /// <summary>
    /// Verifies that JobUpdated event receives both current and previous job information.
    /// </summary>
    [Fact]
    public void JobRegistry_JobUpdated_EventArgs_ContainBothJobs()
    {
        var registry = new JobRegistry();
        var jobId = registry.ScheduleJob("* * * * *", () => { });
        
        JobEventArgs? capturedArgs = null;
        registry.JobUpdated += (_, args) =>
        {
            capturedArgs = args;
        };

        registry.UpdateSchedule(jobId, "*/15 * * * *");

        Assert.NotNull(capturedArgs);
        Assert.Equal(jobId, capturedArgs.Job.Id);
        Assert.Equal("*/15 * * * *", capturedArgs.Job.CronExpression);
        Assert.NotNull(capturedArgs.PreviousJob);
        Assert.Equal("* * * * *", capturedArgs.PreviousJob.CronExpression);
    }

    /// <summary>
    /// Verifies that events are not invoked if there are no subscribers.
    /// </summary>
    [Fact]
    public void JobRegistry_NoEventSubscribers_NoEventInvocation()
    {
        var registry = new JobRegistry();
        
        // No subscribers - operations should work normally
        var jobId = registry.ScheduleJob("* * * * *", () => { });
        Assert.NotEqual(Guid.Empty, jobId);
        
        var updated = registry.UpdateSchedule(jobId, "*/5 * * * *");
        Assert.True(updated);
        
        var removed = registry.RemoveJob(jobId);
        Assert.True(removed);
    }
}
