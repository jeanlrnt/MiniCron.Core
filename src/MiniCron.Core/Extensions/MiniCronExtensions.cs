using Microsoft.Extensions.DependencyInjection;
using MiniCron.Core.Services;

namespace MiniCron.Core.Extensions;

public static class MiniCronExtensions
{
    public static IServiceCollection AddMiniCron(this IServiceCollection services, Action<JobRegistry> configure)
    {
        var registry = new JobRegistry();
        
        configure(registry);

        services.AddLogging();
        
        services.AddSingleton(registry);
        services.AddHostedService<SchedulerBackgroundService>();

        return services;
    }
}