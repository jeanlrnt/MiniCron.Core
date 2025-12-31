# GitHub Copilot Instructions for MiniCron.Core

## Project Overview

MiniCron.Core is a lightweight and simple library for scheduling background tasks in .NET applications using Cron expressions. It integrates seamlessly with the Microsoft.Extensions.DependencyInjection framework.

**Key Features:**
- Simple & Lightweight: Minimal setup required
- Dependency Injection: Access services directly within scheduled jobs
- Async Support: Fully supports asynchronous tasks
- Cancellation Support: Handles cancellation tokens gracefully

## Technology Stack

- **Target Frameworks**: .NET 6.0, 7.0, 8.0, 9.0, and 10.0
- **Primary Language**: C# with nullable reference types enabled
- **Dependencies**: Microsoft.Extensions.Hosting (8.0.1)
- **Testing Framework**: xUnit

## Build and Test Commands

```bash
# Restore dependencies
dotnet restore MiniCron.sln

# Build the solution
dotnet build MiniCron.sln -c Release --no-restore

# Run tests
dotnet test src/MiniCron.Tests/MiniCron.Tests.csproj -c Release --no-build

# Run tests with logging
dotnet test src/MiniCron.Tests/MiniCron.Tests.csproj -c Release --no-build --logger "trx;LogFileName=test_results.trx"
```

## Project Structure

```
src/
├── MiniCron.Core/              # Main library
│   ├── Extensions/             # Extension methods (AddMiniCron)
│   ├── Helpers/                # Helper classes (CronHelper)
│   ├── Models/                 # Data models (CronJob)
│   └── Services/               # Core services (JobRegistry, MiniCronBackgroundService)
└── MiniCron.Tests/             # Unit tests
```

## Code Style and Conventions

### General Guidelines

1. **Nullable Reference Types**: Always enabled - use appropriate nullable annotations
2. **Implicit Usings**: Enabled - common namespaces are automatically imported
3. **File-scoped Namespaces**: Use file-scoped namespace declarations when applicable
4. **Records**: Use `record` types for immutable data models (e.g., `CronJob`)

### Naming Conventions

- Use PascalCase for public members, classes, and methods
- Use camelCase for private fields and local variables
- Use descriptive names that clearly indicate purpose

### Documentation

- Add XML documentation comments (`///`) for public APIs
- Include `<summary>`, `<param>`, `<returns>`, and `<exception>` tags as appropriate
- Keep documentation concise but informative

### Error Handling

- Validate inputs early and throw appropriate exceptions:
  - `ArgumentNullException` for null arguments
  - `ArgumentException` for invalid arguments
- Provide clear, descriptive error messages
- Include context in error messages (e.g., the invalid value and why it's invalid)

## Key Concepts

### Cron Expressions

MiniCron supports standard 5-field cron expressions:

```
* * * * *
| | | | |
| | | | +----- Day of Week (0-6, Sunday=0)
| | | +------- Month (1-12)
| | +--------- Day of Month (1-31)
| +----------- Hour (0-23)
+------------- Minute (0-59)
```

**Supported Syntax:**
- `*` - Wildcard (any value)
- `*/n` - Step values (e.g., `*/5` = every 5 units)
- `n` - Specific value
- `n-m` - Range of values
- `n,m,o` - List of values

### Dependency Injection

- The library uses `IServiceCollection` for registration
- Background service runs as a Singleton
- Jobs receive `IServiceProvider` to resolve services
- Use `CreateScope()` for scoped services (e.g., EF Core DbContext)

### Background Services

- `MiniCronBackgroundService` implements `BackgroundService`
- Runs continuously and executes jobs based on cron schedules
- Supports cancellation tokens for graceful shutdown

## Testing Guidelines

### Test Structure

- Use xUnit as the testing framework
- Organize tests into partial classes when grouping by feature
- Follow the Arrange-Act-Assert pattern

### Test Naming

Use descriptive names that follow the pattern:
```
MethodName_Scenario_ExpectedBehavior
```

Examples:
- `JobRegistry_AddJob_WithInvalidCronExpression_ThrowsArgumentException`
- `AddMiniCron_DefaultOptions_RegistersServicesSuccessfully`

### Test Coverage

Ensure tests cover:
- Valid inputs (happy path)
- Invalid inputs (error cases)
- Edge cases (boundary conditions)
- Null/empty values where applicable

### Validation Tests

When testing validation logic:
- Test with invalid inputs first (negative testing)
- Verify the correct exception type is thrown
- Verify error messages contain relevant information
- Test valid inputs to ensure they don't throw exceptions

## Commit Message Format

Follow the conventional commit format as documented in `.github/git-commit-instructions.md`:

```
<type>(<scope>): <subject> - <file1>, <file2>, ...
```

**Common types:**
- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation changes
- `test`: Adding or updating tests
- `refactor`: Code refactoring
- `perf`: Performance improvements
- `chore`: Maintenance tasks

**Examples:**
- `feat(scheduler): Add support for range syntax in cron expressions - CronHelper.cs`
- `fix(validation): Resolve null reference in cron expression parser - CronHelper.cs`
- `test(cron): Add tests for step syntax validation - MiniCronTests.Validation.cs`

## Additional Resources

- **Documentation**: See `docs/` directory for examples and guides
- **README**: Contains usage examples and quick start guides
- **CI/CD**: GitHub Actions workflows in `.github/workflows/`
  - `ci.yml`: Multi-OS, multi-framework build and test
  - `publish.yml`: NuGet package publishing

## Common Tasks

### Adding a New Feature

1. Implement the feature in the appropriate directory (Extensions, Helpers, Models, or Services)
2. Add XML documentation for public APIs
3. Add comprehensive unit tests
4. Update README.md if the feature changes user-facing behavior
5. Update relevant documentation in `docs/` if needed

### Fixing a Bug

1. Add a failing test that reproduces the bug
2. Fix the bug with minimal changes
3. Verify the test now passes
4. Check for similar issues in related code

### Updating Dependencies

1. Update the PackageReference in the .csproj file
2. Test across all target frameworks
3. Document any breaking changes or migration steps
