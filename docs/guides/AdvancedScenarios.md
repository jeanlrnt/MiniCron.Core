# Advanced Scenarios Guide

This guide covers advanced usage patterns and scenarios for MiniCron.Core.

## Table of Contents

- [Multiple Job Registration Patterns](#multiple-job-registration-patterns)
- [Dynamic Job Configuration](#dynamic-job-configuration)
- [Error Handling Strategies](#error-handling-strategies)
- [Working with Third-Party Services](#working-with-third-party-services)
- [Distributed Systems Considerations](#distributed-systems-considerations)
- [Performance Optimization](#performance-optimization)
- [Testing Strategies](#testing-strategies)

## Multiple Job Registration Patterns

### Organizing Jobs in Separate Methods

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMiniCron(options =>
{
    RegisterDataProcessingJobs(options);
    RegisterMaintenanceJobs(options);
    RegisterNotificationJobs(options);
});

var app = builder.Build();
app.Run();

void RegisterDataProcessingJobs(JobRegistry options)
{
    options.AddJob("*/10 * * * *", async (serviceProvider, cancellationToken) =>
    {
        using var scope = serviceProvider.CreateScope();
        var dataService = scope.ServiceProvider.GetRequiredService<IDataService>();
        await dataService.ProcessDataAsync(cancellationToken);
    });
}

void RegisterMaintenanceJobs(JobRegistry options)
{
    options.AddJob("0 2 * * *", async (serviceProvider, cancellationToken) =>
    {
        using var scope = serviceProvider.CreateScope();
        var cleanupService = scope.ServiceProvider.GetRequiredService<ICleanupService>();
        await cleanupService.PerformCleanupAsync(cancellationToken);
    });
}

void RegisterNotificationJobs(JobRegistry options)
{
    options.AddJob("0 9 * * 1-5", async (serviceProvider, cancellationToken) =>
    {
        using var scope = serviceProvider.CreateScope();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
        await notificationService.SendDailyDigestAsync(cancellationToken);
    });
}
```

### Using Extension Methods for Job Registration

Create a custom extension method to organize jobs:

```csharp
public static class CronJobExtensions
{
    public static IServiceCollection AddScheduledJobs(this IServiceCollection services)
    {
        services.AddMiniCron(options =>
        {
            options.RegisterDataJobs();
            options.RegisterMaintenanceJobs();
            options.RegisterNotificationJobs();
        });
        
        return services;
    }

    private static void RegisterDataJobs(this JobRegistry registry)
    {
        registry.AddJob("*/10 * * * *", async (sp, ct) =>
        {
            using var scope = sp.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IDataProcessingService>();
            await service.ProcessAsync(ct);
        });
    }

    private static void RegisterMaintenanceJobs(this JobRegistry registry)
    {
        registry.AddJob("0 2 * * *", async (sp, ct) =>
        {
            using var scope = sp.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IMaintenanceService>();
            await service.RunMaintenanceAsync(ct);
        });
    }

    private static void RegisterNotificationJobs(this JobRegistry registry)
    {
        registry.AddJob("0 9 * * *", async (sp, ct) =>
        {
            using var scope = sp.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<INotificationService>();
            await service.SendNotificationsAsync(ct);
        });
    }
}

// Usage in Program.cs
builder.Services.AddScheduledJobs();
```

## Dynamic Job Configuration

### Loading Jobs from Database

```csharp
public class ScheduledJobConfiguration
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string CronExpression { get; set; }
    public string JobType { get; set; }
    public bool IsEnabled { get; set; }
    public string Configuration { get; set; } // JSON configuration
}

public static class DynamicJobLoader
{
    public static void LoadJobsFromDatabase(JobRegistry registry, IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        var jobConfigs = dbContext.ScheduledJobs
            .Where(j => j.IsEnabled)
            .ToList();
        
        foreach (var config in jobConfigs)
        {
            registry.AddJob(config.CronExpression, async (sp, ct) =>
            {
                using var jobScope = sp.CreateScope();
                var logger = jobScope.ServiceProvider.GetRequiredService<ILogger<Program>>();
                
                try
                {
                    logger.LogInformation("Executing dynamic job: {JobName}", config.Name);
                    
                    // Execute job based on type
                    switch (config.JobType)
                    {
                        case "DataSync":
                            var syncService = jobScope.ServiceProvider.GetRequiredService<IDataSyncService>();
                            await syncService.SyncAsync(ct);
                            break;
                        
                        case "Report":
                            var reportService = jobScope.ServiceProvider.GetRequiredService<IReportService>();
                            await reportService.GenerateReportAsync(config.Configuration, ct);
                            break;
                        
                        default:
                            logger.LogWarning("Unknown job type: {JobType}", config.JobType);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error executing dynamic job: {JobName}", config.Name);
                }
            });
        }
    }
}

// Usage in Program.cs
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Build temporary service provider to access database
var tempServiceProvider = builder.Services.BuildServiceProvider();

builder.Services.AddMiniCron(options =>
{
    DynamicJobLoader.LoadJobsFromDatabase(options, tempServiceProvider);
});

var app = builder.Build();
app.Run();
```

### Environment-Specific Job Configuration

```csharp
builder.Services.AddMiniCron(options =>
{
    var env = builder.Environment;
    
    if (env.IsDevelopment())
    {
        // More frequent jobs for testing
        options.AddJob("* * * * *", async (sp, ct) =>
        {
            var logger = sp.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Development test job running");
            await Task.CompletedTask;
        });
    }
    else if (env.IsProduction())
    {
        // Production schedules
        options.AddJob("0 0 * * *", async (sp, ct) =>
        {
            using var scope = sp.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IProductionService>();
            await service.RunProductionTaskAsync(ct);
        });
    }
    
    // Common jobs for all environments
    options.AddJob("0 * * * *", async (sp, ct) =>
    {
        using var scope = sp.CreateScope();
        var healthService = scope.ServiceProvider.GetRequiredService<IHealthCheckService>();
        await healthService.PerformHealthCheckAsync(ct);
    });
});
```

## Error Handling Strategies

### Retry Logic with Exponential Backoff

```csharp
public class RetryHelper
{
    public static async Task ExecuteWithRetryAsync(
        Func<Task> operation,
        int maxRetries = 3,
        int delayMilliseconds = 1000,
        ILogger? logger = null)
    {
        for (int i = 0; i <= maxRetries; i++)
        {
            try
            {
                await operation();
                return; // Success
            }
            catch (Exception ex) when (i < maxRetries)
            {
                var delay = delayMilliseconds * (int)Math.Pow(2, i);
                logger?.LogWarning(ex, "Attempt {Attempt} failed. Retrying in {Delay}ms", i + 1, delay);
                await Task.Delay(delay);
            }
        }
    }
}

// Usage in job
builder.Services.AddMiniCron(options =>
{
    options.AddJob("*/10 * * * *", async (serviceProvider, cancellationToken) =>
    {
        using var scope = serviceProvider.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        var apiService = scope.ServiceProvider.GetRequiredService<IExternalApiService>();
        
        await RetryHelper.ExecuteWithRetryAsync(
            async () => await apiService.SyncDataAsync(cancellationToken),
            maxRetries: 3,
            delayMilliseconds: 1000,
            logger: logger
        );
    });
});
```

### Circuit Breaker Pattern

```csharp
public class CircuitBreakerService
{
    private int _failureCount = 0;
    private DateTime _lastFailureTime = DateTime.MinValue;
    private readonly int _threshold = 5;
    private readonly TimeSpan _timeout = TimeSpan.FromMinutes(5);
    
    public bool IsOpen => _failureCount >= _threshold && 
                          DateTime.UtcNow - _lastFailureTime < _timeout;
    
    public void RecordSuccess()
    {
        _failureCount = 0;
    }
    
    public void RecordFailure()
    {
        _failureCount++;
        _lastFailureTime = DateTime.UtcNow;
    }
}

// Register as singleton
builder.Services.AddSingleton<CircuitBreakerService>();

builder.Services.AddMiniCron(options =>
{
    options.AddJob("* * * * *", async (serviceProvider, cancellationToken) =>
    {
        var circuitBreaker = serviceProvider.GetRequiredService<CircuitBreakerService>();
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        
        if (circuitBreaker.IsOpen)
        {
            logger.LogWarning("Circuit breaker is open. Skipping job execution.");
            return;
        }
        
        try
        {
            using var scope = serviceProvider.CreateScope();
            var externalService = scope.ServiceProvider.GetRequiredService<IExternalService>();
            await externalService.CallExternalApiAsync(cancellationToken);
            
            circuitBreaker.RecordSuccess();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Job failed. Recording failure.");
            circuitBreaker.RecordFailure();
        }
    });
});
```

### Dead Letter Queue Pattern

```csharp
public interface IDeadLetterQueue
{
    Task EnqueueFailedJobAsync(string jobName, string errorMessage, Exception exception);
}

public class DeadLetterQueueService : IDeadLetterQueue
{
    private readonly ApplicationDbContext _context;
    
    public DeadLetterQueueService(ApplicationDbContext context)
    {
        _context = context;
    }
    
    public async Task EnqueueFailedJobAsync(string jobName, string errorMessage, Exception exception)
    {
        var failedJob = new FailedJob
        {
            JobName = jobName,
            ErrorMessage = errorMessage,
            Exception = exception.ToString(),
            FailedAt = DateTime.UtcNow
        };
        
        _context.FailedJobs.Add(failedJob);
        await _context.SaveChangesAsync();
    }
}

// Register service
builder.Services.AddScoped<IDeadLetterQueue, DeadLetterQueueService>();

// Use in jobs
builder.Services.AddMiniCron(options =>
{
    options.AddJob("*/10 * * * *", async (serviceProvider, cancellationToken) =>
    {
        using var scope = serviceProvider.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        var dlq = scope.ServiceProvider.GetRequiredService<IDeadLetterQueue>();
        
        try
        {
            var service = scope.ServiceProvider.GetRequiredService<ICriticalService>();
            await service.ProcessAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Critical job failed");
            await dlq.EnqueueFailedJobAsync("CriticalDataProcessing", "Failed to process data", ex);
        }
    });
});
```

## Working with Third-Party Services

### HTTP Client Integration

```csharp
public interface IWeatherService
{
    Task<WeatherData> FetchWeatherDataAsync(CancellationToken cancellationToken);
}

public class WeatherService : IWeatherService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WeatherService> _logger;
    
    public WeatherService(HttpClient httpClient, ILogger<WeatherService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }
    
    public async Task<WeatherData> FetchWeatherDataAsync(CancellationToken cancellationToken)
    {
        var response = await _httpClient.GetAsync("/api/weather", cancellationToken);
        response.EnsureSuccessStatusCode();
        
        return await response.Content.ReadFromJsonAsync<WeatherData>(cancellationToken);
    }
}

// Register HttpClient
builder.Services.AddHttpClient<IWeatherService, WeatherService>(client =>
{
    client.BaseAddress = new Uri("https://api.weather.com");
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddMiniCron(options =>
{
    options.AddJob("0 * * * *", async (serviceProvider, cancellationToken) =>
    {
        using var scope = serviceProvider.CreateScope();
        var weatherService = scope.ServiceProvider.GetRequiredService<IWeatherService>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        
        try
        {
            var weatherData = await weatherService.FetchWeatherDataAsync(cancellationToken);
            logger.LogInformation("Weather data fetched: {Temperature}°C", weatherData.Temperature);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Failed to fetch weather data");
        }
    });
});
```

### Message Queue Integration (RabbitMQ Example)

```csharp
public interface IMessagePublisher
{
    Task PublishAsync<T>(T message, CancellationToken cancellationToken);
}

public class RabbitMQPublisher : IMessagePublisher
{
    private readonly IConnection _connection;
    
    public RabbitMQPublisher(IConnection connection)
    {
        _connection = connection;
    }
    
    public async Task PublishAsync<T>(T message, CancellationToken cancellationToken)
    {
        using var channel = _connection.CreateModel();
        var body = JsonSerializer.SerializeToUtf8Bytes(message);
        
        channel.BasicPublish(
            exchange: "",
            routingKey: "scheduled_tasks",
            basicProperties: null,
            body: body);
        
        await Task.CompletedTask;
    }
}

builder.Services.AddMiniCron(options =>
{
    options.AddJob("*/5 * * * *", async (serviceProvider, cancellationToken) =>
    {
        using var scope = serviceProvider.CreateScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IMessagePublisher>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        
        var message = new ScheduledTaskMessage
        {
            TaskId = Guid.NewGuid(),
            TaskType = "DataSync",
            ScheduledAt = DateTime.UtcNow
        };
        
        await publisher.PublishAsync(message, cancellationToken);
        logger.LogInformation("Published scheduled task message");
    });
});
```

### Redis Cache Integration

```csharp
public interface ICacheWarmer
{
    Task WarmCacheAsync(CancellationToken cancellationToken);
}

public class CacheWarmer : ICacheWarmer
{
    private readonly IDistributedCache _cache;
    private readonly ApplicationDbContext _context;
    
    public CacheWarmer(IDistributedCache cache, ApplicationDbContext context)
    {
        _cache = cache;
        _context = context;
    }
    
    public async Task WarmCacheAsync(CancellationToken cancellationToken)
    {
        var popularItems = await _context.Products
            .OrderByDescending(p => p.ViewCount)
            .Take(100)
            .ToListAsync(cancellationToken);
        
        foreach (var item in popularItems)
        {
            var cacheKey = $"product:{item.Id}";
            var serialized = JsonSerializer.Serialize(item);
            await _cache.SetStringAsync(cacheKey, serialized, cancellationToken);
        }
    }
}

// Register services
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
});
builder.Services.AddScoped<ICacheWarmer, CacheWarmer>();

builder.Services.AddMiniCron(options =>
{
    // Warm cache every hour
    options.AddJob("0 * * * *", async (serviceProvider, cancellationToken) =>
    {
        using var scope = serviceProvider.CreateScope();
        var cacheWarmer = scope.ServiceProvider.GetRequiredService<ICacheWarmer>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        
        logger.LogInformation("Starting cache warming...");
        await cacheWarmer.WarmCacheAsync(cancellationToken);
        logger.LogInformation("Cache warming completed");
    });
});
```

## Distributed Systems Considerations

### Preventing Duplicate Job Execution

When running multiple instances of your application, you need to ensure jobs don't run simultaneously:

```csharp
public interface IDistributedLockService
{
    Task<bool> AcquireLockAsync(string key, TimeSpan expiry, CancellationToken cancellationToken);
    Task ReleaseLockAsync(string key, CancellationToken cancellationToken);
}

public class RedisDistributedLockService : IDistributedLockService
{
    private readonly IDistributedCache _cache;
    
    public RedisDistributedLockService(IDistributedCache cache)
    {
        _cache = cache;
    }
    
    public async Task<bool> AcquireLockAsync(string key, TimeSpan expiry, CancellationToken cancellationToken)
    {
        var lockKey = $"lock:{key}";
        var existingLock = await _cache.GetStringAsync(lockKey, cancellationToken);
        
        if (existingLock != null)
            return false;
        
        await _cache.SetStringAsync(lockKey, "locked", new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiry
        }, cancellationToken);
        
        return true;
    }
    
    public async Task ReleaseLockAsync(string key, CancellationToken cancellationToken)
    {
        var lockKey = $"lock:{key}";
        await _cache.RemoveAsync(lockKey, cancellationToken);
    }
}

// Usage in job
builder.Services.AddScoped<IDistributedLockService, RedisDistributedLockService>();

builder.Services.AddMiniCron(options =>
{
    options.AddJob("*/10 * * * *", async (serviceProvider, cancellationToken) =>
    {
        using var scope = serviceProvider.CreateScope();
        var lockService = scope.ServiceProvider.GetRequiredService<IDistributedLockService>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        
        const string lockKey = "data-processing-job";
        
        if (!await lockService.AcquireLockAsync(lockKey, TimeSpan.FromMinutes(5), cancellationToken))
        {
            logger.LogInformation("Job already running on another instance");
            return;
        }
        
        try
        {
            var dataService = scope.ServiceProvider.GetRequiredService<IDataService>();
            await dataService.ProcessDataAsync(cancellationToken);
            logger.LogInformation("Job completed successfully");
        }
        finally
        {
            await lockService.ReleaseLockAsync(lockKey, cancellationToken);
        }
    });
});
```

## Performance Optimization

### Batch Processing

```csharp
builder.Services.AddMiniCron(options =>
{
    options.AddJob("*/15 * * * *", async (serviceProvider, cancellationToken) =>
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        
        const int batchSize = 1000;
        int processedCount = 0;
        
        while (true)
        {
            var batch = await context.PendingItems
                .Take(batchSize)
                .ToListAsync(cancellationToken);
            
            if (!batch.Any())
                break;
            
            foreach (var item in batch)
            {
                item.Processed = true;
                item.ProcessedAt = DateTime.UtcNow;
            }
            
            await context.SaveChangesAsync(cancellationToken);
            processedCount += batch.Count;
            
            logger.LogInformation("Processed {Count} items", batch.Count);
        }
        
        logger.LogInformation("Total processed: {Total} items", processedCount);
    });
});
```

### Parallel Processing

```csharp
builder.Services.AddMiniCron(options =>
{
    options.AddJob("0 2 * * *", async (serviceProvider, cancellationToken) =>
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        
        var items = await context.Items
            .Where(i => i.NeedsProcessing)
            .ToListAsync(cancellationToken);
        
        await Parallel.ForEachAsync(items, 
            new ParallelOptions 
            { 
                MaxDegreeOfParallelism = 4,
                CancellationToken = cancellationToken
            },
            async (item, ct) =>
            {
                try
                {
                    await ProcessItemAsync(item, ct);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to process item {ItemId}", item.Id);
                }
            });
        
        logger.LogInformation("Parallel processing completed for {Count} items", items.Count);
    });
});
```

## Testing Strategies

### Unit Testing Jobs

```csharp
public class JobTests
{
    [Fact]
    public async Task DataProcessingJob_ShouldProcessData_Successfully()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        
        var mockDataService = new Mock<IDataService>();
        mockDataService
            .Setup(s => s.ProcessDataAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        
        services.AddScoped(_ => mockDataService.Object);
        
        var serviceProvider = services.BuildServiceProvider();
        var cancellationToken = CancellationToken.None;
        
        // Act
        using var scope = serviceProvider.CreateScope();
        var dataService = scope.ServiceProvider.GetRequiredService<IDataService>();
        var result = await dataService.ProcessDataAsync(cancellationToken);
        
        // Assert
        Assert.True(result);
        mockDataService.Verify(s => s.ProcessDataAsync(cancellationToken), Times.Once);
    }
}
```

### Integration Testing

```csharp
public class JobIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    
    public JobIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }
    
    [Fact]
    public async Task ScheduledJob_ShouldExecute_WhenTriggered()
    {
        // Arrange
        var client = _factory.CreateClient();
        
        // Act - Wait for job to execute (for testing purposes)
        await Task.Delay(TimeSpan.FromMinutes(1));
        
        // Assert - Verify job execution through side effects
        var response = await client.GetAsync("/api/job-status");
        response.EnsureSuccessStatusCode();
    }
}
```

## Next Steps

- [Console Application Example](../examples/ConsoleApp.md)
- [Web Application Example](../examples/WebApp.md)
- [Configuration Guide](Configuration.md)
