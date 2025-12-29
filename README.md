# MiniCron.Core

<!-- Continuous Integration (main test/build workflow) -->
[![CI](https://img.shields.io/github/actions/workflow/status/jeanlrnt/MiniCron.Core/ci.yml?label=CI&style=flat-square&logo=github)](https://github.com/jeanlrnt/MiniCron.Core/actions)
![Code Coverage](https://raw.githubusercontent.com/jeanlrnt/MiniCron.Core/main/.github/badges/coverage.svg)
[![Publish](https://img.shields.io/github/actions/workflow/status/jeanlrnt/MiniCron.Core/publish.yml?label=publish&style=flat-square&logo=github)](https://github.com/jeanlrnt/MiniCron.Core/actions/workflows/nuget-publish.yml)
<!-- Latest GitHub release / tag -->
[![Release](https://img.shields.io/github/v/release/jeanlrnt/MiniCron.Core?label=latest%20release&style=flat-square)](https://github.com/jeanlrnt/MiniCron.Core/releases)
[![Release](https://img.shields.io/github/v/release/jeanlrnt/MiniCron.Core?label=pre%20release&style=flat-square)](https://github.com/jeanlrnt/MiniCron.Core/releases)
<!-- NuGet package version and total downloads -->
[![NuGet](https://img.shields.io/nuget/v/MiniCron.Core?label=nuget&style=flat-square)](https://www.nuget.org/packages/MiniCron.Core)
[![NuGet downloads](https://img.shields.io/nuget/dt/MiniCron.Core?style=flat-square)](https://www.nuget.org/packages/MiniCron.Core)
[![dotnet](https://img.shields.io/badge/dotnet-8.0-blue?style=flat-square)](https://dotnet.microsoft.com/)
<!-- Repository info: open issues, contributors, license, last commit -->
[![Open issues](https://img.shields.io/github/issues/jeanlrnt/MiniCron.Core?style=flat-square)](https://github.com/jeanlrnt/MiniCron.Core/issues)
[![Contributors](https://img.shields.io/github/contributors/jeanlrnt/MiniCron.Core?style=flat-square)](https://github.com/jeanlrnt/MiniCron.Core/graphs/contributors)
[![License](https://img.shields.io/github/license/jeanlrnt/MiniCron.Core?style=flat-square)](https://github.com/jeanlrnt/MiniCron.Core/blob/main/LICENSE)
[![Last commit](https://img.shields.io/github/last-commit/jeanlrnt/MiniCron.Core?style=flat-square)](https://github.com/jeanlrnt/MiniCron.Core/commits)

MiniCron.Core is a lightweight and simple library for scheduling background tasks in .NET applications using Cron expressions. It integrates seamlessly with the Microsoft.Extensions.DependencyInjection framework.

## Features

- **Simple & Lightweight**: Minimal setup required.
- **Dependency Injection**: Access your services directly within your scheduled jobs.
- **Async Support**: Fully supports asynchronous tasks.
- **Cancellation Support**: Handles cancellation tokens gracefully.

## Installation

Install the package via NuGet:

```bash
dotnet add package MiniCron.Core
```

## Usage

### 1. Register MiniCron

In your `Program.cs` (or `Startup.cs`), register MiniCron services and define your jobs:

```csharp
using MiniCron.Core.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Register MiniCron
builder.Services.AddMiniCron(options =>
{
    // Run every minute
    options.AddJob("* * * * *", async (serviceProvider, cancellationToken) =>
    {
        Console.WriteLine($"Job executed at {DateTime.Now}");
        await Task.CompletedTask;
    });

    // Run every 5 minutes
    options.AddJob("*/5 * * * *", async (serviceProvider, cancellationToken) =>
    {
        // Resolve services from the DI container
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("5-minute job executed.");
        
        await Task.Delay(100, cancellationToken);
    });
});

var app = builder.Build();
app.Run();
```

### 2. Using Scoped Services

Since the background service runs as a Singleton, you should create a scope to resolve Scoped services (like EF Core DbContexts):

```csharp
builder.Services.AddMiniCron(options =>
{
    options.AddJob("0 12 * * *", async (serviceProvider, cancellationToken) =>
    {
        using var scope = serviceProvider.CreateScope();
        var myScopedService = scope.ServiceProvider.GetRequiredService<IMyScopedService>();
        
        await myScopedService.DoWorkAsync(cancellationToken);
    });
});
```

## Cron Expressions

MiniCron supports standard cron expressions with 5 fields:

```
* * * * *
| | | | |
| | | | +----- Day of Week (0 - 6) (Sunday=0)
| | | +------- Month (1 - 12)
| | +--------- Day of Month (1 - 31)
| +----------- Hour (0 - 23)
+------------- Minute (0 - 59)
```

Examples:
- `* * * * *`: Every minute
- `*/5 * * * *`: Every 5 minutes
- `0 0 * * *`: Every day at midnight
- `0 12 * * 1`: Every Monday at 12:00 PM

## Documentation

Comprehensive guides and examples are available to help you integrate MiniCron.Core into your projects:

### Examples

- **[Console Application Example](docs/examples/ConsoleApp.md)** - Learn how to integrate MiniCron.Core into a minimal .NET Console application with step-by-step instructions and working examples.
- **[Web Application Example](docs/examples/WebApp.md)** - Complete guide for ASP.NET Core applications including Minimal API, MVC, Web API, and Blazor Server examples with Entity Framework Core integration.

### Guides

- **[Configuration Guide](docs/guides/Configuration.md)** - Comprehensive information about configuring MiniCron.Core, including configuration sources, environment-specific settings, and a complete cron expression reference.
- **[Advanced Scenarios Guide](docs/guides/AdvancedScenarios.md)** - Advanced usage patterns including error handling strategies, working with third-party services, distributed systems considerations, and performance optimization.

### Quick Start

**Console Application:**
```bash
dotnet new console -n MyApp
cd MyApp
dotnet add package MiniCron.Core
dotnet add package Microsoft.Extensions.Hosting
```

See the [Console Application Example](docs/examples/ConsoleApp.md) for complete code.

**Web Application:**
```bash
dotnet new web -n MyWebApp
cd MyWebApp
dotnet add package MiniCron.Core
```

See the [Web Application Example](docs/examples/WebApp.md) for complete code.

## Contributing

Contributions are welcome! Please feel free to submit issues, feature requests, or pull requests.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

Made with ❤️ by [jeanlrnt](https://github.com/jeanlrnt)
