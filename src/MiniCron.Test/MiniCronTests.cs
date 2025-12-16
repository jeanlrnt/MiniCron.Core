using Microsoft.Extensions.DependencyInjection;
using MiniCron.Core.Extensions;
using MiniCron.Core.Services;

namespace MiniCron.Test;

public class MiniCronTests
{
    [Fact]
    public void ServiceCollectionExtensions_AddMiniCron_DefaultOptions()
    {
        var services = new ServiceCollection();
        services.AddMiniCron(options => { /* No specific options for this test */ });
        Assert.Contains(services, sd => sd.ServiceType == typeof(JobRegistry));
    }
}