using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

using NexusLabs.Needlr.Injection;

namespace NexusLabs.Needlr.Hosting;

/// <summary>
/// Provides a fluent API for configuring and building host applications using Needlr.
/// Wraps a base Syringe with additional host functionality.
/// </summary>
/// <example>
/// Creating and configuring a HostSyringe:
/// <code>
/// // Method 1: Transition from base Syringe (source-gen by default)
/// var hostSyringe = new Syringe()
///     .ForHost();
/// 
/// // Method 2: Default constructor
/// var hostSyringe = new HostSyringe();
/// 
/// // Build and run the host
/// var host = hostSyringe
///     .UsingOptions(() => CreateHostOptions.Default.UsingArgs(args))
///     .UsingConfigurationCallback((builder, options) => 
///     {
///         // Configure the HostApplicationBuilder
///         builder.Configuration.AddJsonFile("custom-settings.json", optional: true);
///         builder.Services.AddSingleton&lt;IMyCustomService, MyCustomService&gt;();
///     })
///     .BuildHost();
/// 
/// await host.RunAsync();
/// </code>
/// </example>
[DoNotAutoRegister]
public sealed record HostSyringe
{
    internal Syringe BaseSyringe { get; init; } = new();
    internal Func<CreateHostOptions>? OptionsFactory { get; init; }
    internal Func<IServiceProviderBuilder, IServiceCollectionPopulator, IHostFactory>? HostFactoryCreator { get; init; }
    internal Action<HostApplicationBuilder, CreateHostOptions>? ConfigureCallback { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="HostSyringe"/> class.
    /// </summary>
    /// <param name="baseSyringe">The base syringe to wrap.</param>
    /// <example>
    /// <code>
    /// var baseSyringe = new Syringe();
    /// 
    /// var hostSyringe = new HostSyringe(baseSyringe);
    /// </code>
    /// </example>
    public HostSyringe(Syringe baseSyringe)
    {
        ArgumentNullException.ThrowIfNull(baseSyringe);
        BaseSyringe = baseSyringe;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HostSyringe"/> class with default settings.
    /// </summary>
    /// <example>
    /// <code>
    /// var hostSyringe = new HostSyringe();
    /// var host = hostSyringe.BuildHost();
    /// </code>
    /// </example>
    public HostSyringe()
    {
    }

    /// <summary>
    /// Builds a host with the configured settings.
    /// </summary>
    /// <returns>The configured <see cref="IHost"/>.</returns>
    /// <example>
    /// <code>
    /// var host = new Syringe()
    ///     .UsingScrutorTypeRegistrar()
    ///     .ForHost()
    ///     .UsingOptions(() => CreateHostOptions.Default
    ///         .UsingArgs(args)
    ///         .UsingApplicationName("My Worker Service"))
    ///     .UsingConfigurationCallback((builder, options) =>
    ///     {
    ///         // Add custom configuration sources
    ///         builder.Configuration.AddJsonFile("appsettings.local.json", optional: true);
    ///         
    ///         // Register additional services
    ///         builder.Services.AddSingleton&lt;ICustomService, CustomService&gt;();
    ///     })
    ///     .BuildHost();
    /// 
    /// await host.RunAsync();
    /// </code>
    /// </example>
    public IHost BuildHost()
    {
        var typeRegistrar = BaseSyringe.GetOrCreateTypeRegistrar();
        var typeFilterer = BaseSyringe.GetOrCreateTypeFilterer();
        var pluginFactory = BaseSyringe.GetOrCreatePluginFactory();
        var serviceCollectionPopulator = BaseSyringe.GetOrCreateServiceCollectionPopulator(typeRegistrar, typeFilterer, pluginFactory);
        var assemblyProvider = BaseSyringe.GetOrCreateAssemblyProvider();
        var additionalAssemblies = BaseSyringe.GetAdditionalAssemblies();
        var callbacks = BaseSyringe.GetPostPluginRegistrationCallbacks();

        var serviceProviderBuilder = BaseSyringe.GetOrCreateServiceProviderBuilder(
            serviceCollectionPopulator,
            assemblyProvider,
            additionalAssemblies);

        var hostFactory = GetOrCreateHostFactory(serviceProviderBuilder, serviceCollectionPopulator, pluginFactory);
        var options = GetOrCreateOptions();
        if (callbacks.Count > 0)
        {
            options = options.UsingPostPluginRegistrationCallbacks(callbacks);
        }

        return hostFactory.Create(
            options,
            ConfigureCallback);
    }

    /// <summary>
    /// Builds a service provider with the configured settings.
    /// </summary>
    /// <param name="config">The configuration to use for building the service provider.</param>
    /// <returns>The configured <see cref="IServiceProvider"/>.</returns>
    /// <example>
    /// <code>
    /// var config = new ConfigurationBuilder()
    ///     .AddJsonFile("appsettings.json")
    ///     .Build();
    /// 
    /// var serviceProvider = new Syringe()
    ///     .UsingScrutorTypeRegistrar()
    ///     .ForHost()
    ///     .BuildServiceProvider(config);
    /// 
    /// var myService = serviceProvider.GetRequiredService&lt;IMyService&gt;();
    /// </code>
    /// </example>
    public IServiceProvider BuildServiceProvider(IConfiguration config)
    {
        return BaseSyringe.BuildServiceProvider(config);
    }

    private IHostFactory GetOrCreateHostFactory(
        IServiceProviderBuilder serviceProviderBuilder,
        IServiceCollectionPopulator serviceCollectionPopulator,
        IPluginFactory pluginFactory)
    {
        return HostFactoryCreator?.Invoke(serviceProviderBuilder, serviceCollectionPopulator)
            ?? new HostFactory(serviceProviderBuilder, serviceCollectionPopulator, pluginFactory);
    }

    private CreateHostOptions GetOrCreateOptions()
    {
        return OptionsFactory?.Invoke() ?? CreateHostOptions.Default;
    }
}
