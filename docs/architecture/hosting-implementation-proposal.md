# Implementation Proposal: Needlr Hosting Integration

## Overview

This document outlines a concrete implementation proposal for integrating Needlr with Microsoft.Extensions.Hosting patterns while maintaining Needlr's core benefits.

## Proposed Package Structure

```
NexusLabs.Needlr.Hosting/
├── NeedlrHost.cs                    // Entry point class
├── NeedlrHostBuilder.cs             // IHostBuilder implementation  
├── NeedlrHostBuilderExtensions.cs   // Extension methods
├── HostSyringe.cs                   // New Syringe variant for hosting
├── HostSyringeExtensions.cs         // Extension methods for HostSyringe
└── ServiceCollectionExtensions.cs   // Integration with existing hosts
```

## Core Implementation

### 1. NeedlrHost - Primary Entry Point

```csharp
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using NexusLabs.Needlr.Injection;

namespace NexusLabs.Needlr.Hosting;

/// <summary>
/// Provides entry points for creating Needlr-powered hosts compatible with Microsoft.Extensions.Hosting.
/// </summary>
public static class NeedlrHost
{
    /// <summary>
    /// Creates a host builder with Needlr's automatic registration capabilities.
    /// This provides a familiar Microsoft.Extensions.Hosting API while leveraging Needlr's features.
    /// </summary>
    /// <param name="args">Command line arguments</param>
    /// <returns>A configured IHostBuilder</returns>
    /// <example>
    /// <code>
    /// var host = NeedlrHost.CreateDefaultBuilder(args)
    ///     .ConfigureNeedlr(syringe => syringe
    ///         .UsingScrutorTypeRegistrar()
    ///         .UsingAssemblyProvider(builder => builder
    ///             .MatchingAssemblies(x => x.Contains("MyApp"))
    ///             .Build()))
    ///     .ConfigureServices((context, services) =>
    ///     {
    ///         services.AddSingleton<IMyManualService, MyManualService>();
    ///     })
    ///     .Build();
    /// 
    /// await host.RunAsync();
    /// </code>
    /// </example>
    public static IHostBuilder CreateDefaultBuilder(string[]? args = null)
    {
        return new NeedlrHostBuilder()
            .UseNeedlrDefaults()
            .UseCommandLineArguments(args);
    }

    /// <summary>
    /// Creates a host builder with a pre-configured Syringe.
    /// </summary>
    /// <param name="syringe">The pre-configured Syringe to use</param>
    /// <param name="args">Command line arguments</param>
    /// <returns>A configured IHostBuilder</returns>
    /// <example>
    /// <code>
    /// var syringe = new Syringe()
    ///     .UsingScrutorTypeRegistrar()
    ///     .UsingDefaultTypeFilterer();
    /// 
    /// var host = NeedlrHost.CreateBuilder(syringe, args)
    ///     .ConfigureServices((context, services) =>
    ///     {
    ///         services.AddLogging();
    ///     })
    ///     .Build();
    /// </code>
    /// </example>
    public static IHostBuilder CreateBuilder(Syringe syringe, string[]? args = null)
    {
        return new NeedlrHostBuilder(syringe)
            .UseCommandLineArguments(args);
    }
}
```

### 2. HostSyringe - Specialized Syringe for Hosting Scenarios

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NexusLabs.Needlr.Injection;
using System.Reflection;

namespace NexusLabs.Needlr.Hosting;

/// <summary>
/// A specialized Syringe designed for hosting scenarios that provides Microsoft.Extensions.Hosting compatibility
/// while maintaining Needlr's automatic registration capabilities.
/// </summary>
[DoNotAutoRegister]
public sealed record HostSyringe
{
    internal Syringe BaseSyringe { get; init; } = new();
    internal IReadOnlyList<Action<HostBuilderContext, IServiceCollection>>? ServiceConfigurations { get; init; }
    internal IReadOnlyList<Action<HostBuilderContext, IConfigurationBuilder>>? ConfigurationActions { get; init; }
    internal IReadOnlyList<Action<HostBuilderContext, ILoggingBuilder>>? LoggingActions { get; init; }
    internal string[]? Args { get; init; }
    internal string? Environment { get; init; }
    internal string? ContentRoot { get; init; }
    internal Dictionary<string, object>? Properties { get; init; }

    /// <summary>
    /// Builds a full IHost with lifecycle management.
    /// </summary>
    /// <returns>A configured IHost</returns>
    /// <example>
    /// <code>
    /// var host = new Syringe()
    ///     .AsHost()
    ///     .ConfigureServices((context, services) =>
    ///     {
    ///         services.AddHostedService<MyBackgroundService>();
    ///     })
    ///     .BuildHost();
    /// 
    /// await host.StartAsync();
    /// </code>
    /// </example>
    public IHost BuildHost()
    {
        var configuration = BuildConfiguration();
        var context = CreateHostBuilderContext(configuration);
        
        // Create a service collection and populate it directly using Needlr's pattern
        var services = new ServiceCollection();
        
        // Add standard host services
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton(context.HostingEnvironment);
        services.AddOptions();
        services.Configure<HostOptions>(options =>
        {
            options.ShutdownTimeout = TimeSpan.FromSeconds(30);
        });
        
        // Use Needlr's service collection populator to register services directly
        // This follows the same pattern as WebApplicationFactory
        var typeRegistrar = BaseSyringe.GetOrCreateTypeRegistrar();
        var typeFilterer = BaseSyringe.GetOrCreateTypeFilterer();
        var serviceCollectionPopulator = BaseSyringe.GetOrCreateServiceCollectionPopulator(typeRegistrar, typeFilterer);
        var assemblyProvider = BaseSyringe.GetOrCreateAssemblyProvider();
        var additionalAssemblies = BaseSyringe.GetAdditionalAssemblies();
        
        var allAssemblies = assemblyProvider.GetCandidateAssemblies()
            .Concat(additionalAssemblies)
            .Distinct()
            .ToList();
        
        // Let Needlr populate the service collection directly
        serviceCollectionPopulator.RegisterToServiceCollection(services, configuration, allAssemblies);
        
        // Apply any manual service configurations
        if (ServiceConfigurations != null)
        {
            foreach (var serviceConfig in ServiceConfigurations)
            {
                serviceConfig(context, services);
            }
        }
        
        // Execute post-plugin registration callbacks
        var callbacks = BaseSyringe.GetPostPluginRegistrationCallbacks();
        foreach (var callback in callbacks)
        {
            callback(services);
        }
        
        // Build the final service provider
        var finalServiceProvider = services.BuildServiceProvider();
        
        return new NeedlrHost(finalServiceProvider, configuration);
    }

    /// <summary>
    /// Builds just a service provider (compatible with base Syringe behavior).
    /// </summary>
    /// <param name="configuration">Optional configuration to use</param>
    /// <returns>A configured IServiceProvider</returns>
    public IServiceProvider BuildServiceProvider(IConfiguration? configuration = null)
    {
        configuration ??= BuildConfiguration();
        return BaseSyringe.BuildServiceProvider(configuration);
    }

    private IConfiguration BuildConfiguration()
    {
        var builder = new ConfigurationBuilder();
        
        // Add default configuration sources similar to Host.CreateDefaultBuilder
        builder
            .SetBasePath(ContentRoot ?? Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"appsettings.{Environment ?? "Production"}.json", optional: true, reloadOnChange: true);
        
        if (Environment == "Development")
        {
            builder.AddUserSecrets(Assembly.GetEntryAssembly()!);
        }
        
        builder.AddEnvironmentVariables();
        
        if (Args != null)
        {
            builder.AddCommandLine(Args);
        }
        
        var tempConfig = builder.Build();
        var context = CreateHostBuilderContext(tempConfig);
        
        // Apply any configuration actions
        if (ConfigurationActions != null)
        {
            foreach (var configAction in ConfigurationActions)
            {
                configAction(context, builder);
            }
        }
        
        return builder.Build();
    }

    private HostBuilderContext CreateHostBuilderContext(IConfiguration configuration)
    {
        var context = new HostBuilderContext(Properties ?? new Dictionary<string, object>())
        {
            Configuration = configuration,
            HostingEnvironment = CreateHostEnvironment()
        };
        
        return context;
    }

    private IHostEnvironment CreateHostEnvironment()
    {
        // Create a basic host environment
        return new HostEnvironment
        {
            ApplicationName = Assembly.GetEntryAssembly()?.GetName().Name ?? "NeedlrHostedApp",
            ContentRootPath = ContentRoot ?? Directory.GetCurrentDirectory(),
            EnvironmentName = Environment ?? "Production"
        };
    }
}

/// <summary>
/// Basic implementation of IHostEnvironment for Needlr hosts.
/// </summary>
internal class HostEnvironment : IHostEnvironment
{
    public string ApplicationName { get; set; } = string.Empty;
    public IFileProvider ContentRootFileProvider { get; set; } = null!;
    public string ContentRootPath { get; set; } = string.Empty;
    public string EnvironmentName { get; set; } = string.Empty;
}
```

### 3. Extension Methods for Syringe

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NexusLabs.Needlr.Injection;

namespace NexusLabs.Needlr.Hosting;

/// <summary>
/// Extension methods that add Microsoft.Extensions.Hosting compatibility to Syringe.
/// </summary>
public static class SyringeHostingExtensions
{
    /// <summary>
    /// Converts a Syringe to a HostSyringe, enabling hosting-specific functionality.
    /// </summary>
    /// <param name="syringe">The Syringe to convert</param>
    /// <returns>A new HostSyringe</returns>
    /// <example>
    /// <code>
    /// var host = new Syringe()
    ///     .UsingScrutorTypeRegistrar()
    ///     .AsHost()
    ///     .ConfigureServices((context, services) =>
    ///     {
    ///         services.AddHostedService<MyBackgroundService>();
    ///     })
    ///     .BuildHost();
    /// </code>
    /// </example>
    public static HostSyringe AsHost(this Syringe syringe)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        return new HostSyringe { BaseSyringe = syringe };
    }

    /// <summary>
    /// Builds an IHost directly from a Syringe using default hosting configuration.
    /// </summary>
    /// <param name="syringe">The Syringe to build from</param>
    /// <param name="args">Optional command line arguments</param>
    /// <returns>A configured IHost</returns>
    /// <example>
    /// <code>
    /// var host = new Syringe()
    ///     .UsingScrutorTypeRegistrar()
    ///     .BuildHost(args);
    /// 
    /// await host.RunAsync();
    /// </code>
    /// </example>
    public static IHost BuildHost(this Syringe syringe, string[]? args = null)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        return syringe.AsHost().WithCommandLineArguments(args).BuildHost();
    }
}

/// <summary>
/// Extension methods for configuring HostSyringe with hosting-specific options.
/// </summary>
public static class HostSyringeExtensions
{
    /// <summary>
    /// Configures services in a hosting context-aware manner.
    /// </summary>
    /// <param name="hostSyringe">The HostSyringe to configure</param>
    /// <param name="configureServices">The service configuration action</param>
    /// <returns>A new HostSyringe with the service configuration</returns>
    public static HostSyringe ConfigureServices(
        this HostSyringe hostSyringe,
        Action<HostBuilderContext, IServiceCollection> configureServices)
    {
        ArgumentNullException.ThrowIfNull(hostSyringe);
        ArgumentNullException.ThrowIfNull(configureServices);

        var existing = hostSyringe.ServiceConfigurations ?? [];
        var updated = existing.Append(configureServices).ToList();

        return hostSyringe with { ServiceConfigurations = updated };
    }

    /// <summary>
    /// Configures application configuration in a hosting context-aware manner.
    /// </summary>
    /// <param name="hostSyringe">The HostSyringe to configure</param>
    /// <param name="configureConfiguration">The configuration action</param>
    /// <returns>A new HostSyringe with the configuration action</returns>
    public static HostSyringe ConfigureAppConfiguration(
        this HostSyringe hostSyringe,
        Action<HostBuilderContext, IConfigurationBuilder> configureConfiguration)
    {
        ArgumentNullException.ThrowIfNull(hostSyringe);
        ArgumentNullException.ThrowIfNull(configureConfiguration);

        var existing = hostSyringe.ConfigurationActions ?? [];
        var updated = existing.Append(configureConfiguration).ToList();

        return hostSyringe with { ConfigurationActions = updated };
    }

    /// <summary>
    /// Configures logging in a hosting context-aware manner.
    /// </summary>
    /// <param name="hostSyringe">The HostSyringe to configure</param>
    /// <param name="configureLogging">The logging configuration action</param>
    /// <returns>A new HostSyringe with the logging configuration</returns>
    public static HostSyringe ConfigureLogging(
        this HostSyringe hostSyringe,
        Action<HostBuilderContext, ILoggingBuilder> configureLogging)
    {
        ArgumentNullException.ThrowIfNull(hostSyringe);
        ArgumentNullException.ThrowIfNull(configureLogging);

        var existing = hostSyringe.LoggingActions ?? [];
        var updated = existing.Append(configureLogging).ToList();

        return hostSyringe with { LoggingActions = updated };
    }

    /// <summary>
    /// Sets the environment for the host.
    /// </summary>
    /// <param name="hostSyringe">The HostSyringe to configure</param>
    /// <param name="environment">The environment name</param>
    /// <returns>A new HostSyringe with the environment set</returns>
    public static HostSyringe UseEnvironment(this HostSyringe hostSyringe, string environment)
    {
        ArgumentNullException.ThrowIfNull(hostSyringe);
        ArgumentNullException.ThrowIfNull(environment);

        return hostSyringe with { Environment = environment };
    }

    /// <summary>
    /// Sets the content root for the host.
    /// </summary>
    /// <param name="hostSyringe">The HostSyringe to configure</param>
    /// <param name="contentRoot">The content root path</param>
    /// <returns>A new HostSyringe with the content root set</returns>
    public static HostSyringe UseContentRoot(this HostSyringe hostSyringe, string contentRoot)
    {
        ArgumentNullException.ThrowIfNull(hostSyringe);
        ArgumentNullException.ThrowIfNull(contentRoot);

        return hostSyringe with { ContentRoot = contentRoot };
    }

    /// <summary>
    /// Sets command line arguments for the host.
    /// </summary>
    /// <param name="hostSyringe">The HostSyringe to configure</param>
    /// <param name="args">The command line arguments</param>
    /// <returns>A new HostSyringe with the arguments set</returns>
    public static HostSyringe WithCommandLineArguments(this HostSyringe hostSyringe, string[]? args)
    {
        ArgumentNullException.ThrowIfNull(hostSyringe);

        return hostSyringe with { Args = args };
    }
}
```

### 4. Service Collection Extensions for Integration

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NexusLabs.Needlr.Injection;

namespace NexusLabs.Needlr.Hosting;

/// <summary>
/// Extension methods for integrating Needlr into existing Microsoft.Extensions.Hosting applications.
/// </summary>
public static class ServiceCollectionNeedlrExtensions
{
    /// <summary>
    /// Adds Needlr's automatic registration capabilities to an existing service collection.
    /// This allows you to use Needlr within traditional Microsoft.Extensions.Hosting applications.
    /// Note: For better integration, consider using PopulateFromNeedlr during service configuration.
    /// </summary>
    /// <param name="services">The service collection to enhance</param>
    /// <param name="configure">Configuration action for the Syringe</param>
    /// <returns>The service collection for chaining</returns>
    /// <example>
    /// <code>
    /// // Option 1: Direct population (recommended)
    /// var host = Host.CreateDefaultBuilder(args)
    ///     .ConfigureServices((context, services) =>
    ///     {
    ///         services.AddSingleton<IMyManualService, MyManualService>();
    ///         
    ///         var syringe = new Syringe()
    ///             .UsingScrutorTypeRegistrar()
    ///             .UsingAssemblyProvider(builder => builder
    ///                 .MatchingAssemblies(x => x.Contains("MyApp"))
    ///                 .Build());
    ///         
    ///         services.PopulateFromNeedlr(syringe, context.Configuration);
    ///     })
    ///     .Build();
    /// 
    /// // Option 2: Deferred registration (for compatibility)
    /// var host = Host.CreateDefaultBuilder(args)
    ///     .ConfigureServices((context, services) =>
    ///     {
    ///         services.AddSingleton<IMyManualService, MyManualService>();
    ///         
    ///         services.AddNeedlr(syringe => syringe
    ///             .UsingScrutorTypeRegistrar()
    ///             .UsingAssemblyProvider(builder => builder
    ///                 .MatchingAssemblies(x => x.Contains("MyApp"))
    ///                 .Build()));
    ///     })
    ///     .Build();
    /// </code>
    /// </example>
    public static IServiceCollection AddNeedlr(
        this IServiceCollection services,
        Action<Syringe> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        return services.AddNeedlr(configure, _ => { });
    }

    /// <summary>
    /// Adds Needlr's automatic registration capabilities to an existing service collection with configuration access.
    /// </summary>
    /// <param name="services">The service collection to enhance</param>
    /// <param name="configure">Configuration action for the Syringe</param>
    /// <param name="configureWithConfig">Configuration action that receives IConfiguration</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddNeedlr(
        this IServiceCollection services,
        Action<Syringe> configure,
        Action<IConfiguration> configureWithConfig)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        ArgumentNullException.ThrowIfNull(configureWithConfig);

        // Add a registration that will populate services when the host is built
        services.AddSingleton<INeedlrRegistration>(serviceProvider =>
        {
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            configureWithConfig(configuration);
            
            var syringe = new Syringe();
            configure(syringe);
            
            return new NeedlrRegistration(syringe, configuration);
        });

        return services;
    }

    /// <summary>
    /// Adds Needlr's automatic registration capabilities by directly populating the service collection.
    /// This method should be called during the host building process.
    /// </summary>
    /// <param name="services">The service collection to populate</param>
    /// <param name="syringe">The configured Syringe</param>
    /// <param name="configuration">The configuration to use</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection PopulateFromNeedlr(
        this IServiceCollection services,
        Syringe syringe,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(syringe);
        ArgumentNullException.ThrowIfNull(configuration);

        var typeRegistrar = syringe.GetOrCreateTypeRegistrar();
        var typeFilterer = syringe.GetOrCreateTypeFilterer();
        var serviceCollectionPopulator = syringe.GetOrCreateServiceCollectionPopulator(typeRegistrar, typeFilterer);
        var assemblyProvider = syringe.GetOrCreateAssemblyProvider();
        var additionalAssemblies = syringe.GetAdditionalAssemblies();
        
        var allAssemblies = assemblyProvider.GetCandidateAssemblies()
            .Concat(additionalAssemblies)
            .Distinct()
            .ToList();
        
        // Let Needlr populate the service collection directly
        serviceCollectionPopulator.RegisterToServiceCollection(services, configuration, allAssemblies);

        // Execute post-plugin registration callbacks
        var callbacks = syringe.GetPostPluginRegistrationCallbacks();
        foreach (var callback in callbacks)
        {
            callback(services);
        }

        return services;
    }

    /// <summary>
    /// Adds services from a pre-built Needlr service provider to the service collection.
    /// </summary>
    /// <param name="services">The service collection to enhance</param>
    /// <param name="needlrServiceProvider">The Needlr-built service provider</param>
    /// <returns>The service collection for chaining</returns>
    [Obsolete("Use PopulateFromNeedlr instead. This method creates problematic service provider dependencies.")]
    public static IServiceCollection AddNeedlrServices(
        this IServiceCollection services,
        IServiceProvider needlrServiceProvider)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(needlrServiceProvider);

        services.AddSingleton<INeedlrServiceProvider>(new NeedlrServiceProvider(needlrServiceProvider));
        return services;
    }
}

/// <summary>
/// Represents a Needlr registration that can be applied to a service collection.
/// </summary>
public interface INeedlrRegistration
{
    /// <summary>
    /// The configured Syringe.
    /// </summary>
    Syringe Syringe { get; }
    
    /// <summary>
    /// The configuration to use.
    /// </summary>
    IConfiguration Configuration { get; }
}

/// <summary>
/// Implementation of INeedlrRegistration.
/// </summary>
internal class NeedlrRegistration : INeedlrRegistration
{
    public Syringe Syringe { get; }
    public IConfiguration Configuration { get; }

    public NeedlrRegistration(Syringe syringe, IConfiguration configuration)
    {
        Syringe = syringe ?? throw new ArgumentNullException(nameof(syringe));
        Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }
}

/// <summary>
/// Interface for accessing Needlr services within a hosting environment.
/// </summary>
public interface INeedlrServiceProvider
{
    /// <summary>
    /// Gets a service from the Needlr service provider.
    /// </summary>
    /// <typeparam name="T">The service type</typeparam>
    /// <returns>The service instance</returns>
    T GetRequiredService<T>() where T : notnull;

    /// <summary>
    /// Gets a service from the Needlr service provider.
    /// </summary>
    /// <typeparam name="T">The service type</typeparam>
    /// <returns>The service instance or null if not found</returns>
    T? GetService<T>();

    /// <summary>
    /// The underlying Needlr service provider.
    /// </summary>
    IServiceProvider ServiceProvider { get; }
}

/// <summary>
/// Implementation of INeedlrServiceProvider.
/// </summary>
internal class NeedlrServiceProvider : INeedlrServiceProvider
{
    public IServiceProvider ServiceProvider { get; }

    public NeedlrServiceProvider(IServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public T GetRequiredService<T>() where T : notnull
    {
        return ServiceProvider.GetRequiredService<T>();
    }

    public T? GetService<T>()
    {
        return ServiceProvider.GetService<T>();
    }
}
```

## Usage Examples

### Example 1: Simple Console Host
```csharp
using NexusLabs.Needlr.Hosting;
using Microsoft.Extensions.Hosting;

var host = NeedlrHost.CreateDefaultBuilder(args)
    .ConfigureNeedlr(syringe => syringe
        .UsingScrutorTypeRegistrar()
        .UsingAssemblyProvider(builder => builder
            .MatchingAssemblies(x => x.Contains("MyApp"))
            .Build()))
    .Build();

await host.RunAsync();
```

### Example 2: Web Application with Background Services
```csharp
using NexusLabs.Needlr.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

var host = new Syringe()
    .UsingScrutorTypeRegistrar()
    .AsHost()
    .ConfigureServices((context, services) =>
    {
        services.AddHostedService<MyBackgroundService>();
        services.AddLogging();
    })
    .ConfigureAppConfiguration((context, config) =>
    {
        config.AddJsonFile("custom-settings.json", optional: true);
    })
    .BuildHost();

await host.RunAsync();
```

### Example 3: Integration with Existing Host
```csharp
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using NexusLabs.Needlr.Hosting;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        // Traditional service registration
        services.AddSingleton<IMyManualService, MyManualService>();
        
        // Use Needlr to populate services directly into the same service collection
        var syringe = new Syringe()
            .UsingScrutorTypeRegistrar()
            .UsingDefaultTypeFilterer();
            
        services.PopulateFromNeedlr(syringe, context.Configuration);
    })
    .Build();

// Access both traditional and Needlr services normally
var manualService = host.Services.GetRequiredService<IMyManualService>();
var autoService = host.Services.GetRequiredService<IMyAutoService>();
```

## Benefits of This Approach

1. **Familiar API**: Uses Microsoft.Extensions.Hosting patterns developers already know
2. **Incremental Adoption**: Can be added to existing applications gradually
3. **Full Lifecycle Support**: Provides proper host lifecycle management
4. **Backward Compatibility**: Existing Needlr code continues to work unchanged
5. **Best of Both Worlds**: Combines Needlr's automatic registration with standard hosting features

## Migration Path

1. **Phase 1**: Add the package to existing Needlr applications
2. **Phase 2**: Replace `Syringe.BuildServiceProvider()` with `Syringe.BuildHost()`
3. **Phase 3**: Add configuration callbacks for enhanced functionality
4. **Phase 4**: Integrate with existing Microsoft.Extensions.Hosting applications

This approach provides a clear migration path while maintaining all existing functionality.