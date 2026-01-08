# Implementation Guide: Microsoft.Extensions.Hosting Alignment

## Quick Start: Creating the Pull Request

### Step 1: Fork and Clone the Repository

```bash
# Fork the repository on GitHub first, then:
git clone https://github.com/YOUR_USERNAME/needlr.git
cd needlr
```

### Step 2: Create a Feature Branch

```bash
git checkout -b feature/align-with-microsoft-extensions-hosting
```

### Step 3: Add Microsoft.Extensions.Hosting Package

Add the package reference to the appropriate project files:

**For `NexusLabs.Needlr.Injection`:**
```xml
<ItemGroup>
  <PackageReference Include="Microsoft.Extensions.Hosting" />
</ItemGroup>
```

**For `NexusLabs.Needlr.AspNet`:**
```xml
<ItemGroup>
  <PackageReference Include="Microsoft.Extensions.Hosting" />
</ItemGroup>
```

### Step 4: Implement the Integration

See the detailed implementation steps below.

### Step 5: Commit and Push

```bash
git add .
git commit -m "feat: Add Microsoft.Extensions.Hosting integration

- Add HostBuilder extension methods to Syringe
- Support IHostedService registration via plugins
- Expose IHostApplicationLifetime in web applications
- Add ConfigureServices, ConfigureAppConfiguration methods
- Maintain backward compatibility with existing API

Closes #[ISSUE_NUMBER]"

git push origin feature/align-with-microsoft-extensions-hosting
```

### Step 6: Create Pull Request

1. Go to the original repository on GitHub
2. Click "New Pull Request"
3. Select your fork and branch
4. Fill in the PR description (see template below)
5. Submit the PR

## Implementation Steps

### Phase 1: Basic Integration (Recommended Starting Point)

#### 1.1 Create HostBuilder Extension Methods

**File:** `src/NexusLabs.Needlr.Injection/SyringeHostBuilderExtensions.cs`

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace NexusLabs.Needlr.Injection;

/// <summary>
/// Extension methods for integrating Needlr with Microsoft.Extensions.Hosting.
/// </summary>
public static class SyringeHostBuilderExtensions
{
    /// <summary>
    /// Configures the HostBuilder to use Needlr's automatic service discovery.
    /// </summary>
    public static IHostBuilder UseNeedlr(
        this IHostBuilder hostBuilder,
        Syringe syringe)
    {
        ArgumentNullException.ThrowIfNull(hostBuilder);
        ArgumentNullException.ThrowIfNull(syringe);

        return hostBuilder.ConfigureServices((context, services) =>
        {
            var typeRegistrar = syringe.GetOrCreateTypeRegistrar();
            var typeFilterer = syringe.GetOrCreateTypeFilterer();
            var serviceCollectionPopulator = syringe.GetOrCreateServiceCollectionPopulator(
                typeRegistrar, 
                typeFilterer);
            var assemblyProvider = syringe.GetOrCreateAssemblyProvider();
            var additionalAssemblies = syringe.GetAdditionalAssemblies();

            var serviceProviderBuilder = new ServiceProviderBuilder(
                serviceCollectionPopulator,
                assemblyProvider,
                additionalAssemblies);

            // Register Needlr's discovered services
            serviceCollectionPopulator.RegisterToServiceCollection(
                services,
                context.Configuration,
                serviceProviderBuilder.GetCandidateAssemblies());

            // Execute post-plugin registration callbacks
            foreach (var callback in syringe.GetPostPluginRegistrationCallbacks())
            {
                callback.Invoke(services);
            }
        });
    }

    /// <summary>
    /// Converts a Syringe to a HostBuilder with Needlr's auto-discovery enabled.
    /// </summary>
    public static IHostBuilder ToHostBuilder(
        this Syringe syringe,
        string[]? args = null)
    {
        ArgumentNullException.ThrowIfNull(syringe);

        return Host.CreateDefaultBuilder(args)
            .UseNeedlr(syringe);
    }
}
```

#### 1.2 Add HostBuilder-Style Methods to Syringe

**File:** `src/NexusLabs.Needlr.Injection/SyringeHostExtensions.cs`

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace NexusLabs.Needlr.Injection;

/// <summary>
/// Extension methods for configuring Syringe with HostBuilder-style APIs.
/// </summary>
public static class SyringeHostExtensions
{
    /// <summary>
    /// Configures services using a HostBuilder-style callback.
    /// </summary>
    public static Syringe ConfigureServices(
        this Syringe syringe,
        Action<HostBuilderContext, IServiceCollection> configure)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        ArgumentNullException.ThrowIfNull(configure);

        // Store the configuration action to be used when building
        // This requires adding a new property to Syringe record
        return syringe.UsingPostPluginRegistrationCallback(services =>
        {
            // Create a minimal context for the callback
            var context = new HostBuilderContext(new Dictionary<object, object>())
            {
                Configuration = new ConfigurationBuilder().Build(),
                HostingEnvironment = new HostEnvironment()
            };
            configure(context, services);
        });
    }

    /// <summary>
    /// Configures services using a simple callback (without context).
    /// </summary>
    public static Syringe ConfigureServices(
        this Syringe syringe,
        Action<IServiceCollection> configure)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        ArgumentNullException.ThrowIfNull(configure);

        return syringe.UsingPostPluginRegistrationCallback(configure);
    }

    /// <summary>
    /// Adds a hosted service to the service collection.
    /// </summary>
    public static Syringe AddHostedService<THostedService>(
        this Syringe syringe)
        where THostedService : class, IHostedService
    {
        ArgumentNullException.ThrowIfNull(syringe);

        return syringe.UsingPostPluginRegistrationCallback(services =>
        {
            services.AddHostedService<THostedService>();
        });
    }

    /// <summary>
    /// Adds a hosted service to the service collection.
    /// </summary>
    public static Syringe AddHostedService(
        this Syringe syringe,
        Type hostedServiceType)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        ArgumentNullException.ThrowIfNull(hostedServiceType);

        if (!typeof(IHostedService).IsAssignableFrom(hostedServiceType))
        {
            throw new ArgumentException(
                $"Type {hostedServiceType.Name} must implement IHostedService.",
                nameof(hostedServiceType));
        }

        return syringe.UsingPostPluginRegistrationCallback(services =>
        {
            services.Add(ServiceDescriptor.Singleton(typeof(IHostedService), hostedServiceType));
        });
    }
}

// Helper class for minimal HostBuilderContext
internal sealed class HostEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; } = "Production";
    public string ApplicationName { get; set; } = AppDomain.CurrentDomain.FriendlyName;
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(AppContext.BaseDirectory);
}
```

#### 1.3 Expose IHostApplicationLifetime in Web Applications

**File:** `src/NexusLabs.Needlr.AspNet/WebApplicationSyringeHostExtensions.cs`

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;

namespace NexusLabs.Needlr.AspNet;

/// <summary>
/// Extension methods for HostBuilder integration with WebApplicationSyringe.
/// </summary>
public static class WebApplicationSyringeHostExtensions
{
    /// <summary>
    /// Configures the web application to use HostBuilder-style service configuration.
    /// </summary>
    public static WebApplicationSyringe ConfigureServices(
        this WebApplicationSyringe syringe,
        Action<HostBuilderContext, IServiceCollection> configure)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        ArgumentNullException.ThrowIfNull(configure);

        return syringe with
        {
            BaseSyringe = syringe.BaseSyringe.ConfigureServices(configure)
        };
    }

    /// <summary>
    /// Adds a hosted service to the web application.
    /// </summary>
    public static WebApplicationSyringe AddHostedService<THostedService>(
        this WebApplicationSyringe syringe)
        where THostedService : class, IHostedService
    {
        ArgumentNullException.ThrowIfNull(syringe);

        return syringe with
        {
            BaseSyringe = syringe.BaseSyringe.AddHostedService<THostedService>()
        };
    }
}
```

### Phase 2: Enhanced Integration (Optional)

#### 2.1 Add ConfigureAppConfiguration Support

This would require more significant changes to the Syringe record to store configuration callbacks.

#### 2.2 Add ConfigureLogging Support

Similar to configuration, this would require extending the Syringe record.

## Testing

### Unit Tests

Create test files:

**File:** `src/NexusLabs.Needlr.Injection.Tests/SyringeHostBuilderExtensionsTests.cs`

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace NexusLabs.Needlr.Injection.Tests;

public sealed class SyringeHostBuilderExtensionsTests
{
    [Fact]
    public void UseNeedlr_ConfiguresHostBuilder_WithAutoDiscovery()
    {
        var syringe = new Syringe()
            .UsingDefaultTypeRegistrar()
            .UsingDefaultTypeFilterer();

        var host = Host.CreateDefaultBuilder()
            .UseNeedlr(syringe)
            .Build();

        Assert.NotNull(host);
        Assert.NotNull(host.Services);
    }

    [Fact]
    public void ToHostBuilder_CreatesHostBuilder_WithNeedlrIntegration()
    {
        var syringe = new Syringe()
            .UsingDefaultTypeRegistrar();

        var hostBuilder = syringe.ToHostBuilder();
        var host = hostBuilder.Build();

        Assert.NotNull(host);
    }

    [Fact]
    public void AddHostedService_RegistersHostedService()
    {
        var syringe = new Syringe()
            .AddHostedService<TestHostedService>();

        var serviceProvider = syringe.BuildServiceProvider(
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());

        var hostedService = serviceProvider.GetService<IHostedService>();
        Assert.NotNull(hostedService);
        Assert.IsType<TestHostedService>(hostedService);
    }

    private sealed class TestHostedService : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
```

## Pull Request Template

```markdown
## Description

This PR adds integration between Needlr and Microsoft.Extensions.Hosting, aligning Needlr's API surface with Microsoft's hosting patterns while maintaining Needlr's unique automatic service discovery capabilities.

## Changes

- ✅ Added `UseNeedlr()` extension method for HostBuilder
- ✅ Added `ToHostBuilder()` extension method for Syringe
- ✅ Added `ConfigureServices()` methods aligned with HostBuilder pattern
- ✅ Added `AddHostedService()` support
- ✅ Maintained backward compatibility with existing API
- ✅ Added unit tests for new functionality

## Related Issue

Closes #[ISSUE_NUMBER]

## Type of Change

- [ ] Bug fix
- [x] New feature
- [ ] Breaking change
- [ ] Documentation update

## Testing

- [x] Unit tests added/updated
- [x] All existing tests pass
- [x] Manual testing completed

## Checklist

- [x] Code follows project style guidelines
- [x] Self-review completed
- [x] Comments added for complex code
- [x] Documentation updated
- [x] No new warnings generated
- [x] Tests added/updated
- [x] All tests pass
```

## Next Steps After PR is Merged

1. **Gather Feedback**: Monitor how the community uses the new integration
2. **Documentation**: Update README and docs with examples
3. **Phase 2 Planning**: Based on feedback, plan additional API alignment
4. **Consider Deprecation**: If Phase 2 methods are well-received, consider deprecating old methods with migration guidance

## Notes

- Start with Phase 1 implementation as it provides value without breaking changes
- Maintain all existing extension methods for backward compatibility
- Consider adding `[Obsolete]` attributes in future phases if deprecating methods
- Ensure all tests pass before submitting PR
- Update documentation with examples of new usage patterns

