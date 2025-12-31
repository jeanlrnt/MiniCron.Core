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
        // JobRegistry dependencies (including ILogger<JobRegistry>) are automatically resolved by DI
        services.AddSingleton<JobRegistry>();
        services.AddSingleton<ISystemClock, SystemClock>();
        services.AddSingleton<IJobLockProvider, InMemoryJobLockProvider>();

        // Hosted service
        services.AddHostedService<MiniCronBackgroundService>();

        return services;
    }

    /// <summary>
    /// Backwards-compatible overload that accepts a <see cref="JobRegistry"/> initializer.
    /// <para>
    /// <b>Note:</b> Do not use this method together with <see cref="AddMiniCronOptions"/>.
    /// Both methods register ISystemClock and IJobLockProvider as singletons, leading to duplicate registrations.
    /// Use either this method (for legacy registry-based initialization) or <see cref="AddMiniCronOptions"/> (for modern configuration-based initialization).
    /// </para>
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