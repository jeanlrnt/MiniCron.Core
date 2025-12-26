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
}
