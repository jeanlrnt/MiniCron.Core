using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MiniCron.Core.Services;
using MiniCron.Core.Models;

namespace MiniCron.Core.Extensions;

public static class MiniCronExtensions
{
    /// <summary>
    /// Register MiniCron with optional configuration.
    /// Use <see cref="AddMiniCron(IServiceCollection, Action{JobRegistry})"/> for the legacy registry-based initializer.
    /// </summary>
    public static IServiceCollection AddMiniCronOptions(this IServiceCollection services, Action<MiniCronOptions>? configure = null)
    {
        if (configure != null)
        {
            services.Configure(configure);
        }

        services.AddLogging();

        // Core services
        services.AddSingleton<JobRegistry>();
        services.AddSingleton<ISystemClock, SystemClock>();
        services.AddSingleton<IJobLockProvider, InMemoryJobLockProvider>();

        // Hosted service
        services.AddHostedService<MiniCronBackgroundService>();

        return services;
    }

    /// <summary>
    /// Backwards-compatible overload that accepts a <see cref="JobRegistry"/> initializer.
    /// </summary>
    public static IServiceCollection AddMiniCron(this IServiceCollection services, Action<JobRegistry> configure)
    {
        services.AddLogging();
        
        services.AddSingleton<JobRegistry>(sp =>
        {
            var logger = sp.GetService<ILogger<JobRegistry>>();
            var registry = new JobRegistry(logger);
            configure(registry);
            return registry;
        });
        
        services.AddSingleton<ISystemClock, SystemClock>();
        services.AddSingleton<IJobLockProvider, InMemoryJobLockProvider>();
        services.AddHostedService<MiniCronBackgroundService>();

        return services;
    }
}