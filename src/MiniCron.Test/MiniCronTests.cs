using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MiniCron.Core.Extensions;
using MiniCron.Core.Services;

namespace MiniCron.Test;

public partial class MiniCronTests
{
    [Fact]
    public void ServiceCollectionExtensions_AddMiniCron_DefaultOptions()
    {
        var services = new ServiceCollection();
        services.AddMiniCron(options => { /* No specific options for this test */ });
        Assert.Contains(services, sd => sd.ServiceType == typeof(JobRegistry));
        Assert.Contains(services, sd => sd.ServiceType == typeof(IHostedService) && sd.ImplementationType == typeof(MiniCronBackgroundService));
    }

    [Fact]
    public void JobRegistry_AddJob_WithInvalidCronExpression_ThrowsArgumentException()
    {
        // Arrange
        var registry = new JobRegistry();
        var action = (IServiceProvider sp, CancellationToken ct) => Task.CompletedTask;

        // Act & Assert - Invalid expression with too few fields
        var exception = Assert.Throws<ArgumentException>(() => 
            registry.AddJob("* * *", action));
        
        Assert.Contains("must have exactly 5 fields", exception.Message);
    }

    [Fact]
    public void JobRegistry_AddJob_WithValidCronExpression_DoesNotThrow()
    {
        // Arrange
        var registry = new JobRegistry();
        var action = (IServiceProvider sp, CancellationToken ct) => Task.CompletedTask;

        // Act
        var exception = Record.Exception(() => 
            registry.AddJob("*/5 * * * *", action));

        // Assert
        Assert.Null(exception);
        Assert.Single(registry.GetJobs());
    }

    [Fact]
    public void JobRegistry_AddJob_WithInvalidStepSyntax_ThrowsArgumentException()
    {
        // Arrange
        var registry = new JobRegistry();
        var action = (IServiceProvider sp, CancellationToken ct) => Task.CompletedTask;

        // Act & Assert - Invalid step syntax "5/3" instead of "*/3"
        var exception = Assert.Throws<ArgumentException>(() => 
            registry.AddJob("5/3 * * * *", action));
        
        Assert.Contains("Step syntax must start with '*'", exception.Message);
    }
}