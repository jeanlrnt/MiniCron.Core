# Console Application Example

This guide demonstrates how to integrate MiniCron.Core into a minimal .NET Console application.

## Prerequisites

- .NET 8.0 or later
- Basic knowledge of C# and dependency injection

## Step-by-Step Integration

### 1. Create a New Console Application

```bash
dotnet new console -n MiniCronConsoleApp
cd MiniCronConsoleApp
```

### 2. Install Required Packages

```bash
dotnet add package MiniCron.Core
dotnet add package Microsoft.Extensions.Hosting
dotnet add package Microsoft.Extensions.Logging
```

### 3. Minimal Console Application Example

Here's a complete minimal example:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MiniCron.Core.Extensions;

var builder = Host.CreateApplicationBuilder(args);

// Configure logging
builder.Logging.AddConsole();

// Register MiniCron with scheduled jobs
builder.Services.AddMiniCron(options =>
{
    // Run every minute
    options.AddJob("* * * * *", async (serviceProvider, cancellationToken) =>
    {
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Job executed at {Time}", DateTime.Now);
        await Task.CompletedTask;
    });

    // Run every 5 minutes
    options.AddJob("*/5 * * * *", async (serviceProvider, cancellationToken) =>
    {
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("5-minute job executed at {Time}", DateTime.Now);
        await Task.CompletedTask;
    });
});

var app = builder.Build();
await app.RunAsync();
```

### Quick: Using overloads and subscribing to job events

If you prefer the registry-style initializer (also supported), you can register jobs using the ergonomic overloads and subscribe to lifecycle events:

```csharp
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddMiniCron(registry =>
{
    // Subscribe to registry events
    registry.JobAdded += (s, e) => Console.WriteLine($"Job added: {e.Job.Id} {e.Job.CronExpression}");
    registry.JobRemoved += (s, e) => Console.WriteLine($"Job removed: {e.Job.Id}");
    registry.JobUpdated += (s, e) => Console.WriteLine($"Job updated: {e.Job.Id} {e.PreviousJob?.CronExpression} -> {e.Job.CronExpression}");

    // Use overload that accepts CancellationToken-aware delegate
    registry.ScheduleJob("*/5 * * * *", async (ct) =>
    {
        Console.WriteLine("Running token-aware job every 5 minutes");
        await Task.CompletedTask;
    });

    // Use simple synchronous Action overload
    registry.ScheduleJob("* * * * *", () => Console.WriteLine("Simple action every minute"));

    // Legacy-style delegate that receives IServiceProvider
    registry.ScheduleJob("0 * * * *", (sp, ct) =>
    {
        var logger = sp?.GetService<ILogger<Program>>();
        logger?.LogInformation("Hourly job executed");
        return Task.CompletedTask;
    });
});

var app = builder.Build();
await app.RunAsync();
```

### 4. Run the Application

```bash
dotnet run
```

You should see output like:

```
info: Program[0]
      Job executed at 12/18/2025 11:25:00 AM
info: Program[0]
      5-minute job executed at 12/18/2025 11:25:00 AM
info: Program[0]
      Job executed at 12/18/2025 11:26:00 AM
```

## Advanced Console Example with Custom Services

### Define a Custom Service

```csharp
public interface INotificationService
{
    Task SendNotificationAsync(string message, CancellationToken cancellationToken);
}

public class ConsoleNotificationService : INotificationService
{
    private readonly ILogger<ConsoleNotificationService> _logger;

    public ConsoleNotificationService(ILogger<ConsoleNotificationService> logger)
    {
        _logger = logger;
    }

    public async Task SendNotificationAsync(string message, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Notification: {Message}", message);
        await Task.Delay(100, cancellationToken); // Simulate async work
    }
}
```

### Register and Use the Service

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MiniCron.Core.Extensions;

var builder = Host.CreateApplicationBuilder(args);

// Configure logging
builder.Logging.AddConsole();

// Register custom services
builder.Services.AddSingleton<INotificationService, ConsoleNotificationService>();

// Register MiniCron with scheduled jobs
builder.Services.AddMiniCron(options =>
{
    // Daily notification at 9:00 AM
    options.AddJob("0 9 * * *", async (serviceProvider, cancellationToken) =>
    {
        var notificationService = serviceProvider.GetRequiredService<INotificationService>();
        await notificationService.SendNotificationAsync("Good morning! Daily task started.", cancellationToken);
    });

    // Hourly status check
    options.AddJob("0 * * * *", async (serviceProvider, cancellationToken) =>
    {
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Hourly status check completed");
        await Task.CompletedTask;
    });
});

var app = builder.Build();
await app.RunAsync();
```

## Configuration from appsettings.json

You can also load cron expressions from configuration:

### appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  },
  "CronJobs": {
    "DailyReport": "0 9 * * *",
    "HourlyCheck": "0 * * * *",
    "EveryMinute": "* * * * *"
  }
}
```

### Program.cs

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MiniCron.Core.Extensions;

var builder = Host.CreateApplicationBuilder(args);

// Configure logging
builder.Logging.AddConsole();

// Register MiniCron with scheduled jobs from configuration
builder.Services.AddMiniCron(options =>
{
    var config = builder.Configuration;

    // Daily report
    var dailyReportCron = config["CronJobs:DailyReport"] ?? "0 9 * * *";
    options.AddJob(dailyReportCron, async (serviceProvider, cancellationToken) =>
    {
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Generating daily report...");
        await Task.CompletedTask;
    });

    // Hourly check
    var hourlyCheckCron = config["CronJobs:HourlyCheck"] ?? "0 * * * *";
    options.AddJob(hourlyCheckCron, async (serviceProvider, cancellationToken) =>
    {
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Performing hourly check...");
        await Task.CompletedTask;
    });
});

var app = builder.Build();
await app.RunAsync();
```

## Testing Cron Jobs

For testing purposes, you can use frequent cron expressions. Note that standard cron expressions support minutes as the smallest time unit:

```csharp
// Test job that runs every minute (smallest interval supported)
options.AddJob("* * * * *", async (serviceProvider, cancellationToken) =>
{
    var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Test job executed");
    await Task.CompletedTask;
});

// If you need sub-minute testing during development, 
// consider using a test framework or mock time
```

## Common Cron Expressions for Console Apps

| Expression | Description |
|------------|-------------|
| `* * * * *` | Every minute |
| `*/5 * * * *` | Every 5 minutes |
| `0 * * * *` | Every hour |
| `0 */6 * * *` | Every 6 hours |
| `0 0 * * *` | Daily at midnight |
| `0 9 * * 1-5` | Weekdays at 9 AM |
| `0 2 * * 0` | Every Sunday at 2 AM |

## Best Practices

1. **Use Dependency Injection**: Always resolve services from the `serviceProvider` parameter
2. **Handle Cancellation**: Respect the `cancellationToken` in your async operations
3. **Logging**: Use structured logging for better diagnostics
4. **Error Handling**: Wrap job logic in try-catch blocks for resilience
5. **Keep Jobs Small**: Break complex tasks into smaller, focused jobs
6. **Configuration**: Store cron expressions in configuration files for flexibility

## Complete Working Example

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MiniCron.Core.Extensions;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddConsole();

builder.Services.AddMiniCron(options =>
{
    // Simple job
    options.AddJob("* * * * *", async (serviceProvider, cancellationToken) =>
    {
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        
        try
        {
            logger.LogInformation("Starting scheduled task at {Time}", DateTime.Now);
            
            // Your business logic here
            await Task.Delay(1000, cancellationToken);
            
            logger.LogInformation("Scheduled task completed successfully");
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Task was cancelled");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing scheduled task");
        }
    });
});

var app = builder.Build();
await app.RunAsync();
```

## Next Steps

- [Web Application Example](WebApp.md)
- [Advanced Scenarios Guide](../guides/AdvancedScenarios.md)
- [Configuration Guide](../guides/Configuration.md)
