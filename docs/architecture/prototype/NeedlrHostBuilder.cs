using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NexusLabs.Needlr.Injection;

namespace NexusLabs.Needlr.Hosting;

/// <summary>
/// Prototype implementation of a Needlr-powered IHostBuilder.
/// This demonstrates how Needlr can integrate with Microsoft.Extensions.Hosting patterns.
/// </summary>
public class NeedlrHostBuilder : IHostBuilder
{
    private readonly Syringe _baseSyringe;
    private readonly List<Action<HostBuilderContext, IServiceCollection>> _serviceConfigurations = new();
    private readonly List<Action<HostBuilderContext, IConfigurationBuilder>> _configurationActions = new();
    private readonly List<Action<HostBuilderContext, ILoggingBuilder>> _loggingActions = new();
    private readonly Dictionary<string, object> _properties = new();
    
    private string? _environment;
    private string? _contentRoot;
    private string[]? _args;

    public IDictionary<object, object> Properties => new Dictionary<object, object>(_properties.ToDictionary(kvp => (object)kvp.Key, kvp => kvp.Value));

    public NeedlrHostBuilder(Syringe? baseSyringe = null)
    {
        _baseSyringe = baseSyringe ?? new Syringe();
    }

    public IHost Build()
    {
        // Build configuration first
        var configBuilder = new ConfigurationBuilder();
        
        // Add default configuration sources (similar to Host.CreateDefaultBuilder)
        configBuilder
            .SetBasePath(_contentRoot ?? Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"appsettings.{_environment ?? "Production"}.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables();

        if (_args != null)
        {
            configBuilder.AddCommandLine(_args);
        }

        var tempConfiguration = configBuilder.Build();
        var context = new HostBuilderContext(_properties)
        {
            Configuration = tempConfiguration,
            HostingEnvironment = CreateHostEnvironment()
        };

        // Apply configuration actions
        foreach (var configAction in _configurationActions)
        {
            configAction(context, configBuilder);
        }

        var finalConfiguration = configBuilder.Build();
        context.Configuration = finalConfiguration;

        // Build the Needlr service provider first to get automatic registrations
        var needlrServiceProvider = _baseSyringe.BuildServiceProvider(finalConfiguration);

        // Create a service collection and add Needlr services
        var services = new ServiceCollection();
        
        // Add configuration and environment
        services.AddSingleton<IConfiguration>(finalConfiguration);
        services.AddSingleton(context.HostingEnvironment);
        
        // Add logging
        services.AddLogging(builder =>
        {
            foreach (var loggingAction in _loggingActions)
            {
                loggingAction(context, builder);
            }
        });
        
        // Add default host services
        services.AddOptions();
        services.Configure<HostOptions>(options =>
        {
            options.ShutdownTimeout = TimeSpan.FromSeconds(30);
        });

        // Add the Needlr service provider as a service
        services.AddSingleton<INeedlrServiceProvider>(new NeedlrServiceProvider(needlrServiceProvider));

        // Apply additional service configurations
        foreach (var serviceConfig in _serviceConfigurations)
        {
            serviceConfig(context, services);
        }

        // Build the final service provider
        var finalServiceProvider = services.BuildServiceProvider();

        return new NeedlrHost(finalServiceProvider);
    }

    public IHostBuilder ConfigureAppConfiguration(Action<HostBuilderContext, IConfigurationBuilder> configureDelegate)
    {
        ArgumentNullException.ThrowIfNull(configureDelegate);
        _configurationActions.Add(configureDelegate);
        return this;
    }

    public IHostBuilder ConfigureContainer<TContainerBuilder>(Action<HostBuilderContext, TContainerBuilder> configureDelegate)
    {
        // For simplicity, we don't support custom containers in this prototype
        // In a full implementation, this would integrate with the service collection
        throw new NotSupportedException("Custom containers are not supported in this prototype.");
    }

    public IHostBuilder ConfigureHostConfiguration(Action<IConfigurationBuilder> configureDelegate)
    {
        ArgumentNullException.ThrowIfNull(configureDelegate);
        
        // Host configuration actions are applied before the context is created
        _configurationActions.Insert(0, (_, builder) => configureDelegate(builder));
        return this;
    }

    public IHostBuilder ConfigureLogging(Action<HostBuilderContext, ILoggingBuilder> configureLogging)
    {
        ArgumentNullException.ThrowIfNull(configureLogging);
        _loggingActions.Add(configureLogging);
        return this;
    }

    public IHostBuilder ConfigureServices(Action<HostBuilderContext, IServiceCollection> configureServices)
    {
        ArgumentNullException.ThrowIfNull(configureServices);
        _serviceConfigurations.Add(configureServices);
        return this;
    }

    public IHostBuilder UseContentRoot(string contentRoot)
    {
        _contentRoot = contentRoot ?? throw new ArgumentNullException(nameof(contentRoot));
        return this;
    }

    public IHostBuilder UseEnvironment(string environment)
    {
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        return this;
    }

    public IHostBuilder UseServiceProviderFactory<TContainerBuilder>(IServiceProviderFactory<TContainerBuilder> factory) where TContainerBuilder : notnull
    {
        // For simplicity, we don't support custom service provider factories in this prototype
        throw new NotSupportedException("Custom service provider factories are not supported in this prototype.");
    }

    public IHostBuilder UseServiceProviderFactory<TContainerBuilder>(Func<HostBuilderContext, IServiceProviderFactory<TContainerBuilder>> factory) where TContainerBuilder : notnull
    {
        // For simplicity, we don't support custom service provider factories in this prototype
        throw new NotSupportedException("Custom service provider factories are not supported in this prototype.");
    }

    // Extension method support
    public NeedlrHostBuilder UseNeedlrDefaults()
    {
        // Configure common Needlr defaults
        return this;
    }

    public NeedlrHostBuilder UseCommandLineArguments(string[]? args)
    {
        _args = args;
        return this;
    }

    public NeedlrHostBuilder ConfigureNeedlr(Action<Syringe> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        
        var configuredSyringe = _baseSyringe;
        configure(configuredSyringe);
        
        return new NeedlrHostBuilder(configuredSyringe);
    }

    private IHostEnvironment CreateHostEnvironment()
    {
        return new HostEnvironment
        {
            ApplicationName = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name ?? "NeedlrApp",
            ContentRootPath = _contentRoot ?? Directory.GetCurrentDirectory(),
            EnvironmentName = _environment ?? "Production"
        };
    }
}

/// <summary>
/// Simple implementation of IHost that wraps a service provider.
/// </summary>
internal class NeedlrHost : IHost
{
    private readonly IServiceProvider _serviceProvider;
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    public IServiceProvider Services => _serviceProvider;

    public NeedlrHost(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        // Start hosted services
        var hostedServices = _serviceProvider.GetServices<IHostedService>();
        foreach (var service in hostedServices)
        {
            await service.StartAsync(cancellationToken);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        // Stop hosted services
        var hostedServices = _serviceProvider.GetServices<IHostedService>();
        foreach (var service in hostedServices.Reverse())
        {
            await service.StopAsync(cancellationToken);
        }
        
        _cancellationTokenSource.Cancel();
    }

    public void Dispose()
    {
        _cancellationTokenSource.Dispose();
        
        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}

/// <summary>
/// Simple implementation of IHostEnvironment.
/// </summary>
internal class HostEnvironment : IHostEnvironment
{
    public string ApplicationName { get; set; } = string.Empty;
    public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    public string ContentRootPath { get; set; } = string.Empty;
    public string EnvironmentName { get; set; } = string.Empty;
}

/// <summary>
/// Interface for accessing Needlr services within a hosting environment.
/// </summary>
public interface INeedlrServiceProvider
{
    T GetRequiredService<T>() where T : notnull;
    T? GetService<T>();
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