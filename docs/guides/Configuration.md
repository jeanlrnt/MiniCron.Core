# Configuration Guide

This guide provides comprehensive information about configuring MiniCron.Core for various scenarios.

## Table of Contents

- [Basic Configuration](#basic-configuration)
- [Configuration Sources](#configuration-sources)
- [Environment-Specific Settings](#environment-specific-settings)
- [Cron Expression Reference](#cron-expression-reference)
- [Service Registration](#service-registration)
- [Logging Configuration](#logging-configuration)
- [Best Practices](#best-practices)

## Basic Configuration

### Minimal Setup

The simplest way to configure MiniCron.Core:

```csharp
using MiniCron.Core.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMiniCron(options =>
{
    options.AddJob("* * * * *", async (serviceProvider, cancellationToken) =>
    {
        // Job logic here
        await Task.CompletedTask;
    });
});

var app = builder.Build();
app.Run();
```

### Multiple Jobs Setup

Register multiple jobs with different schedules:

```csharp
builder.Services.AddMiniCron(options =>
{
    // Run every minute
    options.AddJob("* * * * *", async (sp, ct) =>
    {
        var logger = sp.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Minute job executing");
        await Task.CompletedTask;
    });

    // Run every hour
    options.AddJob("0 * * * *", async (sp, ct) =>
    {
        var logger = sp.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Hourly job executing");
        await Task.CompletedTask;
    });

    // Run daily at midnight
    options.AddJob("0 0 * * *", async (sp, ct) =>
    {
        using var scope = sp.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IDailyService>();
        await service.RunDailyTaskAsync(ct);
    });
});
```

## Configuration Sources

### Using appsettings.json

Store cron expressions in your configuration file:

**appsettings.json**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "MiniCron": "Debug"
    }
  },
  "JobSchedules": {
    "DataSync": "*/10 * * * *",
    "DatabaseCleanup": "0 2 * * *",
    "ReportGeneration": "0 8 * * 1-5",
    "CacheRefresh": "*/15 * * * *",
    "BackupJob": "0 3 * * 0"
  },
  "JobSettings": {
    "EnableDataSync": true,
    "EnableCleanup": true,
    "EnableReports": true
  }
}
```

**Program.cs**
```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMiniCron(options =>
{
    var config = builder.Configuration;
    var jobSchedules = config.GetSection("JobSchedules");
    var jobSettings = config.GetSection("JobSettings");

    // Data sync job
    if (jobSettings.GetValue<bool>("EnableDataSync"))
    {
        var schedule = jobSchedules["DataSync"] ?? "*/10 * * * *";
        options.AddJob(schedule, async (sp, ct) =>
        {
            using var scope = sp.CreateScope();
            var syncService = scope.ServiceProvider.GetRequiredService<IDataSyncService>();
            await syncService.SyncAsync(ct);
        });
    }

    // Database cleanup job
    if (jobSettings.GetValue<bool>("EnableCleanup"))
    {
        var schedule = jobSchedules["DatabaseCleanup"] ?? "0 2 * * *";
        options.AddJob(schedule, async (sp, ct) =>
        {
            using var scope = sp.CreateScope();
            var cleanupService = scope.ServiceProvider.GetRequiredService<ICleanupService>();
            await cleanupService.CleanupAsync(ct);
        });
    }

    // Report generation job
    if (jobSettings.GetValue<bool>("EnableReports"))
    {
        var schedule = jobSchedules["ReportGeneration"] ?? "0 8 * * 1-5";
        options.AddJob(schedule, async (sp, ct) =>
        {
            using var scope = sp.CreateScope();
            var reportService = scope.ServiceProvider.GetRequiredService<IReportService>();
            await reportService.GenerateReportsAsync(ct);
        });
    }
});

var app = builder.Build();
app.Run();
```

### Environment Variables

Use environment variables for sensitive or environment-specific configurations:

```csharp
builder.Services.AddMiniCron(options =>
{
    // Read from environment variable
    var cronSchedule = Environment.GetEnvironmentVariable("BACKUP_SCHEDULE") ?? "0 3 * * *";
    
    options.AddJob(cronSchedule, async (sp, ct) =>
    {
        using var scope = sp.CreateScope();
        var backupService = scope.ServiceProvider.GetRequiredService<IBackupService>();
        await backupService.PerformBackupAsync(ct);
    });
});
```

### User Secrets (Development)

For development, use user secrets:

```bash
dotnet user-secrets init
dotnet user-secrets set "JobSchedules:DataSync" "* * * * *"
```

Then access in code:
```csharp
builder.Services.AddMiniCron(options =>
{
    var schedule = builder.Configuration["JobSchedules:DataSync"] ?? "*/10 * * * *";
    options.AddJob(schedule, async (sp, ct) =>
    {
        using var scope = sp.CreateScope();
        var syncService = scope.ServiceProvider.GetRequiredService<IDataSyncService>();
        await syncService.SyncAsync(ct);
    });
});
```

## Environment-Specific Settings

### Using Environment-Specific Configuration Files

**appsettings.Development.json**
```json
{
  "JobSchedules": {
    "DataSync": "* * * * *",
    "DatabaseCleanup": "*/5 * * * *"
  }
}
```

**appsettings.Production.json**
```json
{
  "JobSchedules": {
    "DataSync": "*/10 * * * *",
    "DatabaseCleanup": "0 2 * * *"
  }
}
```

### Conditional Job Registration

```csharp
builder.Services.AddMiniCron(options =>
{
    var env = builder.Environment;

    if (env.IsDevelopment())
    {
        // Frequent testing schedule
        options.AddJob("* * * * *", async (sp, ct) =>
        {
            var logger = sp.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Development test job");
            await Task.CompletedTask;
        });
    }

    if (env.IsStaging())
    {
        // Staging-specific jobs
        options.AddJob("*/5 * * * *", async (sp, ct) =>
        {
            using var scope = sp.CreateScope();
            var testService = scope.ServiceProvider.GetRequiredService<ITestService>();
            await testService.RunStagingTestsAsync(ct);
        });
    }

    if (env.IsProduction())
    {
        // Production schedules
        options.AddJob("0 2 * * *", async (sp, ct) =>
        {
            using var scope = sp.CreateScope();
            var backupService = scope.ServiceProvider.GetRequiredService<IBackupService>();
            await backupService.BackupDatabaseAsync(ct);
        });
    }

    // Jobs for all environments
    options.AddJob("0 * * * *", async (sp, ct) =>
    {
        using var scope = sp.CreateScope();
        var healthService = scope.ServiceProvider.GetRequiredService<IHealthService>();
        await healthService.CheckHealthAsync(ct);
    });
});
```

## Cron Expression Reference

### Format

MiniCron.Core uses the standard 5-field cron format:

```
* * * * *
│ │ │ │ │
│ │ │ │ └─── Day of Week (0-6, Sunday=0)
│ │ │ └───── Month (1-12)
│ │ └─────── Day of Month (1-31)
│ └───────── Hour (0-23)
└─────────── Minute (0-59)
```

### Common Patterns

#### Basic Intervals

```csharp
// Every minute
"* * * * *"

// Every 5 minutes
"*/5 * * * *"

// Every 10 minutes
"*/10 * * * *"

// Every 15 minutes
"*/15 * * * *"

// Every 30 minutes
"*/30 * * * *"

// Every hour
"0 * * * *"

// Every 2 hours
"0 */2 * * *"

// Every 6 hours
"0 */6 * * *"
```

#### Daily Schedules

```csharp
// Every day at midnight
"0 0 * * *"

// Every day at 3 AM
"0 3 * * *"

// Every day at 6 AM
"0 6 * * *"

// Every day at noon
"0 12 * * *"

// Every day at 6 PM
"0 18 * * *"

// Twice a day (9 AM and 9 PM)
"0 9,21 * * *"
```

#### Weekly Schedules

```csharp
// Every Monday at 9 AM
"0 9 * * 1"

// Every Friday at 5 PM
"0 17 * * 5"

// Weekdays (Monday-Friday) at 8 AM
"0 8 * * 1-5"

// Weekends (Saturday-Sunday) at 10 AM
"0 10 * * 0,6"

// Every Sunday at 2 AM
"0 2 * * 0"
```

#### Monthly Schedules

```csharp
// First day of every month at midnight
"0 0 1 * *"

// 15th of every month at 3 AM
"0 3 15 * *"

// Last day of every month (approximation - use day 28)
"0 0 28 * *"

// First Monday of every month at 9 AM
// Note: Requires custom logic, cron doesn't support this directly
```

#### Complex Schedules

```csharp
// Every 5 minutes during business hours (9 AM - 5 PM, weekdays)
"*/5 9-17 * * 1-5"

// Every hour on weekdays
"0 * * * 1-5"

// At 30 minutes past every hour on weekdays
"30 * * * 1-5"

// Every 2 hours during the day (8 AM - 8 PM)
"0 8-20/2 * * *"

// Multiple specific times
"0 8,12,16,20 * * *"

// First and last day of month at midnight
"0 0 1,28 * *"
```

### Special Cases

```csharp
// Beginning of every quarter (Jan 1, Apr 1, Jul 1, Oct 1)
"0 0 1 1,4,7,10 *"

// End of business day on last Friday of month
// Note: Requires custom logic for "last Friday"

// Every weekday at 9:30 AM
"30 9 * * 1-5"

// Every 15 minutes between 9 AM and 5 PM on weekdays
"*/15 9-17 * * 1-5"
```

### Validation

You can validate cron expressions programmatically:

```csharp
using MiniCron.Core.Helpers;

public bool IsValidCronExpression(string expression)
{
    try
    {
        // CronHelper will throw if invalid
        CronHelper.ValidateCronExpression(expression);
        return true;
    }
    catch
    {
        return false;
    }
}
```

## Service Registration

### Service Lifetimes

Understanding service lifetimes is crucial when working with MiniCron:

```csharp
// Singleton services (safe to use directly)
builder.Services.AddSingleton<ICacheService, CacheService>();

// Scoped services (need scope creation)
builder.Services.AddScoped<IDbContext, ApplicationDbContext>();
builder.Services.AddScoped<IOrderService, OrderService>();

// Transient services (need scope creation)
builder.Services.AddTransient<IEmailService, EmailService>();

builder.Services.AddMiniCron(options =>
{
    options.AddJob("* * * * *", async (serviceProvider, cancellationToken) =>
    {
        // ✅ Singleton - can use directly
        var cacheService = serviceProvider.GetRequiredService<ICacheService>();
        
        // ✅ Scoped/Transient - create scope
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IDbContext>();
        var orderService = scope.ServiceProvider.GetRequiredService<IOrderService>();
        
        // Use services
        await orderService.ProcessOrdersAsync(cancellationToken);
    });
});
```

### Registering Job-Specific Services

```csharp
// Define a job handler interface
public interface IScheduledJobHandler
{
    Task ExecuteAsync(CancellationToken cancellationToken);
}

// Implement specific handlers
public class DataSyncJobHandler : IScheduledJobHandler
{
    private readonly IDataService _dataService;
    private readonly ILogger<DataSyncJobHandler> _logger;

    public DataSyncJobHandler(IDataService dataService, ILogger<DataSyncJobHandler> logger)
    {
        _dataService = dataService;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting data synchronization");
        await _dataService.SyncAsync(cancellationToken);
        _logger.LogInformation("Data synchronization completed");
    }
}

// Register handlers
builder.Services.AddScoped<DataSyncJobHandler>();
builder.Services.AddScoped<CleanupJobHandler>();
builder.Services.AddScoped<ReportJobHandler>();

// Use in MiniCron
builder.Services.AddMiniCron(options =>
{
    options.AddJob("*/10 * * * *", async (sp, ct) =>
    {
        using var scope = sp.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<DataSyncJobHandler>();
        await handler.ExecuteAsync(ct);
    });
});
```

## Logging Configuration

### Basic Logging Setup

```csharp
var builder = WebApplication.CreateBuilder(args);

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Set log levels
builder.Logging.SetMinimumLevel(LogLevel.Information);

builder.Services.AddMiniCron(options =>
{
    options.AddJob("* * * * *", async (serviceProvider, cancellationToken) =>
    {
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Job started at {Time}", DateTime.Now);
        
        // Job logic
        
        logger.LogInformation("Job completed at {Time}", DateTime.Now);
        await Task.CompletedTask;
    });
});
```

### Structured Logging

```csharp
builder.Services.AddMiniCron(options =>
{
    options.AddJob("*/10 * * * *", async (serviceProvider, cancellationToken) =>
    {
        using var scope = serviceProvider.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        
        using (logger.BeginScope(new Dictionary<string, object>
        {
            ["JobName"] = "DataSync",
            ["ExecutionId"] = Guid.NewGuid()
        }))
        {
            logger.LogInformation("Job execution started");
            
            try
            {
                var service = scope.ServiceProvider.GetRequiredService<IDataService>();
                var result = await service.ProcessAsync(cancellationToken);
                
                logger.LogInformation("Job completed successfully. Processed {Count} items", result.Count);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Job execution failed");
            }
        }
    });
});
```

### Using Serilog

```bash
dotnet add package Serilog.AspNetCore
dotnet add package Serilog.Sinks.Console
dotnet add package Serilog.Sinks.File
```

```csharp
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/minicron-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddMiniCron(options =>
{
    options.AddJob("*/5 * * * *", async (serviceProvider, cancellationToken) =>
    {
        using var scope = serviceProvider.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        
        logger.LogInformation("Job {JobName} executing at {Time}", "DataSync", DateTime.Now);
        
        // Job logic
        
        await Task.CompletedTask;
    });
});

var app = builder.Build();

try
{
    Log.Information("Starting application");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
```

## Best Practices

### 1. Use Configuration Files

Store cron expressions in configuration files rather than hardcoding them:

```csharp
// ❌ Bad - Hardcoded
options.AddJob("0 2 * * *", async (sp, ct) => { /* ... */ });

// ✅ Good - Configurable
var schedule = builder.Configuration["JobSchedules:Backup"] ?? "0 2 * * *";
options.AddJob(schedule, async (sp, ct) => { /* ... */ });
```

### 2. Always Create Scopes for Scoped Services

```csharp
// ❌ Bad - Direct access to scoped service
options.AddJob("* * * * *", async (serviceProvider, cancellationToken) =>
{
    var dbContext = serviceProvider.GetRequiredService<ApplicationDbContext>();
    // This will fail!
});

// ✅ Good - Create scope
options.AddJob("* * * * *", async (serviceProvider, cancellationToken) =>
{
    using var scope = serviceProvider.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    // This works!
});
```

### 3. Handle Errors Gracefully

```csharp
options.AddJob("*/10 * * * *", async (serviceProvider, cancellationToken) =>
{
    using var scope = serviceProvider.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    try
    {
        // Job logic
    }
    catch (OperationCanceledException)
    {
        logger.LogWarning("Job was cancelled");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Job failed with error");
    }
});
```

### 4. Use Descriptive Job Names in Logs

```csharp
options.AddJob("0 2 * * *", async (serviceProvider, cancellationToken) =>
{
    using var scope = serviceProvider.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    const string jobName = "DatabaseCleanup";
    logger.LogInformation("{JobName} started", jobName);
    
    try
    {
        // Job logic
        logger.LogInformation("{JobName} completed successfully", jobName);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "{JobName} failed", jobName);
    }
});
```

### 5. Respect Cancellation Tokens

```csharp
options.AddJob("* * * * *", async (serviceProvider, cancellationToken) =>
{
    // ✅ Pass cancellation token to all async operations
    await Task.Delay(1000, cancellationToken);
    await dbContext.SaveChangesAsync(cancellationToken);
    await httpClient.GetAsync(url, cancellationToken);
});
```

### 6. Keep Jobs Focused

```csharp
// ❌ Bad - Too much in one job
options.AddJob("0 2 * * *", async (sp, ct) =>
{
    await CleanupDatabase(sp, ct);
    await GenerateReports(sp, ct);
    await SendEmails(sp, ct);
    await BackupData(sp, ct);
});

// ✅ Good - Separate jobs
options.AddJob("0 2 * * *", async (sp, ct) => await CleanupDatabase(sp, ct));
options.AddJob("0 3 * * *", async (sp, ct) => await GenerateReports(sp, ct));
options.AddJob("0 4 * * *", async (sp, ct) => await SendEmails(sp, ct));
options.AddJob("0 5 * * *", async (sp, ct) => await BackupData(sp, ct));
```

### 7. Document Your Cron Schedules

```csharp
builder.Services.AddMiniCron(options =>
{
    // Data synchronization - runs every 10 minutes
    // Syncs data from external API to local database
    options.AddJob("*/10 * * * *", async (sp, ct) =>
    {
        // Implementation
    });

    // Daily cleanup - runs at 2 AM every day
    // Removes old records and optimizes database
    options.AddJob("0 2 * * *", async (sp, ct) =>
    {
        // Implementation
    });

    // Weekly backup - runs at 3 AM every Sunday
    // Creates full database backup
    options.AddJob("0 3 * * 0", async (sp, ct) =>
    {
        // Implementation
    });
});
```

## Next Steps

- [Console Application Example](../examples/ConsoleApp.md)
- [Web Application Example](../examples/WebApp.md)
- [Advanced Scenarios Guide](AdvancedScenarios.md)
