using MiniCron.Core.Services;

namespace MiniCron.Tests;

public partial class MiniCronTests
{
    [Fact]
    public void JobRegistry_Overloads_And_Events_Fire()
    {
        var registry = new JobRegistry();

        bool added = false, removed = false, updated = false;
        registry.JobAdded += (_, _) => added = true;
        registry.JobRemoved += (_, _) => removed = true;
        registry.JobUpdated += (_, _) => updated = true;

        // Schedule using CancellationToken-aware overload
        var id1 = registry.ScheduleJob("* * * * *", _ => Task.CompletedTask);
        Assert.True(added);

        // Update schedule
        var ok = registry.UpdateSchedule(id1, "*/5 * * * *");
        Assert.True(ok);
        Assert.True(updated);

        // Add another via Action overload
        var id2 = registry.ScheduleJob("0 * * * *", () => { });
        Assert.NotEqual(id1, id2);

        // Remove job
        var removedOk = registry.RemoveJob(id1);
        Assert.True(removedOk);
        Assert.True(removed);
    }
}
