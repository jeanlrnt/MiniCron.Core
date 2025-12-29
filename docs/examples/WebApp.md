# Web Application Example

This guide demonstrates how to integrate MiniCron.Core into ASP.NET Core web applications.

## Prerequisites

- .NET 8.0 or later
- ASP.NET Core knowledge
- Understanding of dependency injection and scoped services

## Step-by-Step Integration

### 1. Create a New Web Application

```bash
# Minimal API
dotnet new web -n MiniCronWebApp

# MVC Application
dotnet new mvc -n MiniCronMvcApp

# Web API
dotnet new webapi -n MiniCronApiApp
```

### 2. Install MiniCron.Core

```bash
cd MiniCronWebApp
dotnet add package MiniCron.Core
```

### 3. Minimal Web API Example

Here's a complete example with ASP.NET Core Minimal API:

```csharp
using MiniCron.Core.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register MiniCron with background jobs
builder.Services.AddMiniCron(options =>
{
    // Cache cleanup every hour
    options.AddJob("0 * * * *", async (serviceProvider, cancellationToken) =>
    {
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Running cache cleanup at {Time}", DateTime.Now);
        
        // Cleanup logic here
        await Task.CompletedTask;
    });

    // Health check every 5 minutes
    options.AddJob("*/5 * * * *", async (serviceProvider, cancellationToken) =>
    {
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Performing health check");
        await Task.CompletedTask;
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/", () => "MiniCron Web App is running!");

app.Run();
```

### Quick: Registry overloads and event hooks

You can subscribe to registry events and use the ergonomic overloads when registering MiniCron:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMiniCron(registry =>
{
    registry.JobAdded += (s, e) => builder.Logging.CreateLogger("MiniCron").LogInformation($"Job added: {e.Job.Id} {e.Job.CronExpression}");

    // Token-aware job
    registry.ScheduleJob("*/10 * * * *", async (ct) =>
    {
        // perform async work with cancellation
        await Task.Delay(10, ct);
    });

    // Simple synchronous action
    registry.ScheduleJob("0 * * * *", () => Console.WriteLine("Hourly maintenance task"));
});

var app = builder.Build();
app.Run();
```

## Working with Entity Framework Core

### Setup with DbContext

```csharp
using Microsoft.EntityFrameworkCore;
using MiniCron.Core.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Register DbContext
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register application services
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IEmailService, EmailService>();

// Register MiniCron
builder.Services.AddMiniCron(options =>
{
    // Process pending orders every 10 minutes
    options.AddJob("*/10 * * * *", async (serviceProvider, cancellationToken) =>
    {
        // IMPORTANT: Create a scope for scoped services
        using var scope = serviceProvider.CreateScope();
        var orderService = scope.ServiceProvider.GetRequiredService<IOrderService>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        
        try
        {
            logger.LogInformation("Processing pending orders...");
            await orderService.ProcessPendingOrdersAsync(cancellationToken);
            logger.LogInformation("Pending orders processed successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing pending orders");
        }
    });

    // Send daily summary emails at 8 AM
    options.AddJob("0 8 * * *", async (serviceProvider, cancellationToken) =>
    {
        using var scope = serviceProvider.CreateScope();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        
        try
        {
            logger.LogInformation("Sending daily summary emails...");
            await emailService.SendDailySummaryAsync(cancellationToken);
            logger.LogInformation("Daily summary emails sent");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending daily summary emails");
        }
    });
});

var app = builder.Build();
app.Run();
```

### Example DbContext and Services

```csharp
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) 
        : base(options)
    {
    }

    public DbSet<Order> Orders { get; set; }
    public DbSet<User> Users { get; set; }
}

public interface IOrderService
{
    Task ProcessPendingOrdersAsync(CancellationToken cancellationToken);
}

public class OrderService : IOrderService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<OrderService> _logger;

    public OrderService(ApplicationDbContext context, ILogger<OrderService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task ProcessPendingOrdersAsync(CancellationToken cancellationToken)
    {
        var pendingOrders = await _context.Orders
            .Where(o => o.Status == OrderStatus.Pending)
            .ToListAsync(cancellationToken);

        foreach (var order in pendingOrders)
        {
            _logger.LogInformation("Processing order {OrderId}", order.Id);
            // Process order logic
            order.Status = OrderStatus.Processing;
        }

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Processed {Count} pending orders", pendingOrders.Count);
    }
}
```

## MVC Application Example

For MVC applications, you can add MiniCron in `Program.cs`:

```csharp
using MiniCron.Core.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllersWithViews();

// Add your application services
builder.Services.AddScoped<IReportService, ReportService>();

// Register MiniCron
builder.Services.AddMiniCron(options =>
{
    // Generate daily reports at 7 AM
    options.AddJob("0 7 * * *", async (serviceProvider, cancellationToken) =>
    {
        using var scope = serviceProvider.CreateScope();
        var reportService = scope.ServiceProvider.GetRequiredService<IReportService>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        
        logger.LogInformation("Generating daily reports...");
        await reportService.GenerateDailyReportsAsync(cancellationToken);
        logger.LogInformation("Daily reports generated");
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
```

## Blazor Server Application

```csharp
using MiniCron.Core.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// Add your services
builder.Services.AddSingleton<WeatherForecastService>();
builder.Services.AddScoped<IDataRefreshService, DataRefreshService>();

// Register MiniCron
builder.Services.AddMiniCron(options =>
{
    // Refresh data cache every 15 minutes
    options.AddJob("*/15 * * * *", async (serviceProvider, cancellationToken) =>
    {
        using var scope = serviceProvider.CreateScope();
        var dataRefreshService = scope.ServiceProvider.GetRequiredService<IDataRefreshService>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        
        logger.LogInformation("Refreshing data cache...");
        await dataRefreshService.RefreshCacheAsync(cancellationToken);
        logger.LogInformation("Data cache refreshed");
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
```

## Real-World Scenarios

### 1. API Rate Limit Reset

```csharp
builder.Services.AddSingleton<IRateLimitService, RateLimitService>();

builder.Services.AddMiniCron(options =>
{
    // Reset API rate limits every hour
    options.AddJob("0 * * * *", async (serviceProvider, cancellationToken) =>
    {
        var rateLimitService = serviceProvider.GetRequiredService<IRateLimitService>();
        await rateLimitService.ResetHourlyLimitsAsync(cancellationToken);
    });
});
```

### 2. Session Cleanup

```csharp
builder.Services.AddMiniCron(options =>
{
    // Clean up expired sessions every 30 minutes
    options.AddJob("*/30 * * * *", async (serviceProvider, cancellationToken) =>
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        
        var expiryDate = DateTime.UtcNow.AddDays(-7);
        var expiredSessions = await dbContext.UserSessions
            .Where(s => s.LastActivity < expiryDate)
            .ToListAsync(cancellationToken);
        
        dbContext.UserSessions.RemoveRange(expiredSessions);
        await dbContext.SaveChangesAsync(cancellationToken);
        
        logger.LogInformation("Cleaned up {Count} expired sessions", expiredSessions.Count);
    });
});
```

### 3. File Cleanup

```csharp
builder.Services.AddMiniCron(options =>
{
    // Clean up temporary files every day at 2 AM
    options.AddJob("0 2 * * *", async (serviceProvider, cancellationToken) =>
    {
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        var tempPath = Path.Combine(Directory.GetCurrentDirectory(), "temp");
        
        if (Directory.Exists(tempPath))
        {
            var files = Directory.GetFiles(tempPath);
            var oldFiles = files.Where(f => 
                File.GetCreationTime(f) < DateTime.Now.AddDays(-7));
            
            foreach (var file in oldFiles)
            {
                File.Delete(file);
            }
            
            logger.LogInformation("Cleaned up {Count} old temporary files", oldFiles.Count());
        }
        
        await Task.CompletedTask;
    });
});
```

### 4. Database Backup

```csharp
builder.Services.AddMiniCron(options =>
{
    // Backup database daily at 3 AM
    options.AddJob("0 3 * * *", async (serviceProvider, cancellationToken) =>
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        
        try
        {
            var backupPath = config["BackupPath"] ?? "./backups";
            Directory.CreateDirectory(backupPath);
            
            var fileName = $"backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}.bak";
            var fullPath = Path.GetFullPath(Path.Combine(backupPath, fileName));
            
            // Validate path to prevent directory traversal attacks
            if (!fullPath.StartsWith(Path.GetFullPath(backupPath)))
            {
                logger.LogError("Invalid backup path detected");
                return;
            }
            
            // Execute backup command (SQL Server example)
            // IMPORTANT: This is a simplified example. In production:
            // 1. Use stored procedures with proper permissions
            // 2. Use dedicated backup tools or Azure Backup services
            // 3. Implement proper access control and auditing
            // 4. Consider using SQL Server Agent for scheduling
            
            // For this example, we'll use a stored procedure approach:
            // CREATE PROCEDURE sp_BackupDatabase @BackupPath NVARCHAR(500)
            // AS BEGIN
            //     DECLARE @sql NVARCHAR(1000);
            //     SET @sql = 'BACKUP DATABASE [YourDB] TO DISK = ''' + @BackupPath + '''';
            //     EXEC sp_executesql @sql;
            // END
            
            // Call the stored procedure
            await dbContext.Database.ExecuteSqlRawAsync(
                "EXEC sp_BackupDatabase @p0",
                fullPath,
                cancellationToken);
            
            logger.LogInformation("Database backup created: {FileName}", fileName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database backup failed");
        }
    });
});
```

### 5. Notification System

```csharp
builder.Services.AddScoped<INotificationService, NotificationService>();

builder.Services.AddMiniCron(options =>
{
    // Send reminder notifications at 9 AM on weekdays
    options.AddJob("0 9 * * 1-5", async (serviceProvider, cancellationToken) =>
    {
        using var scope = serviceProvider.CreateScope();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        
        try
        {
            await notificationService.SendDailyRemindersAsync(cancellationToken);
            logger.LogInformation("Daily reminders sent successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send daily reminders");
        }
    });
});
```

## Configuration Best Practices

### Using appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=MyApp;Trusted_Connection=True;"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "MiniCron": "Debug"
    }
  },
  "ScheduledJobs": {
    "OrderProcessing": "*/10 * * * *",
    "DailySummary": "0 8 * * *",
    "CacheCleanup": "0 */6 * * *",
    "DatabaseBackup": "0 3 * * *"
  }
}
```

### Loading from Configuration

```csharp
builder.Services.AddMiniCron(options =>
{
    var config = builder.Configuration.GetSection("ScheduledJobs");
    
    // Order processing job
    options.AddJob(config["OrderProcessing"] ?? "*/10 * * * *", async (sp, ct) =>
    {
        using var scope = sp.CreateScope();
        var orderService = scope.ServiceProvider.GetRequiredService<IOrderService>();
        await orderService.ProcessPendingOrdersAsync(ct);
    });
    
    // Daily summary job
    options.AddJob(config["DailySummary"] ?? "0 8 * * *", async (sp, ct) =>
    {
        using var scope = sp.CreateScope();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
        await emailService.SendDailySummaryAsync(ct);
    });
});
```

## Important Considerations

### 1. Service Lifetime

MiniCron runs as a singleton background service. Always create a scope for scoped services:

```csharp
options.AddJob("* * * * *", async (serviceProvider, cancellationToken) =>
{
    // ✅ CORRECT: Create scope for scoped services (DbContext, repositories, etc.)
    using var scope = serviceProvider.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    
    // Use the scoped service
    await dbContext.SaveChangesAsync(cancellationToken);
});
```

### 2. Error Handling

Always wrap job logic in try-catch blocks:

```csharp
options.AddJob("0 * * * *", async (serviceProvider, cancellationToken) =>
{
    using var scope = serviceProvider.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    try
    {
        // Job logic here
    }
    catch (OperationCanceledException)
    {
        logger.LogWarning("Job was cancelled");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Job execution failed");
    }
});
```

### 3. Cancellation Token

Respect the cancellation token for graceful shutdown:

```csharp
options.AddJob("* * * * *", async (serviceProvider, cancellationToken) =>
{
    // Pass cancellation token to async operations
    await SomeAsyncOperation(cancellationToken);
    await Task.Delay(1000, cancellationToken);
});
```

## Performance Tips

1. **Use Singleton Services for Read-Only Operations**: If your job only reads data and doesn't need a DbContext, use singleton services
2. **Batch Operations**: Process items in batches to avoid memory issues
3. **Use Pagination**: For large datasets, process data in pages
4. **Monitor Memory Usage**: Long-running jobs should be mindful of memory
5. **Logging**: Use structured logging for better insights

## Common Cron Schedules for Web Apps

| Expression | Use Case |
|------------|----------|
| `*/5 * * * *` | Cache refresh, health checks |
| `*/15 * * * *` | Data synchronization |
| `0 * * * *` | Hourly reports, cleanup |
| `0 */6 * * *` | Regular maintenance tasks |
| `0 0 * * *` | Daily reports, analytics |
| `0 2 * * *` | Database backups, cleanup |
| `0 8 * * 1-5` | Weekday notifications |

## Next Steps

- [Console Application Example](ConsoleApp.md)
- [Advanced Scenarios Guide](../guides/AdvancedScenarios.md)
- [Configuration Guide](../guides/Configuration.md)
