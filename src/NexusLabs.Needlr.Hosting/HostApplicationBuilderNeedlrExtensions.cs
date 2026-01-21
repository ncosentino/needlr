using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using NexusLabs.Needlr.Injection;

using System.Reflection;

namespace NexusLabs.Needlr.Hosting;

/// <summary>
/// Extension methods for integrating Needlr discovery into user-controlled <see cref="HostApplicationBuilder"/> instances.
/// Use this when you want to maintain control over the host creation process while still benefiting from Needlr's
/// automatic service discovery and plugin system.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="UseNeedlrDiscovery"/> provides a "reverse integration" approach where Needlr adapts to your builder
/// rather than you adapting to Needlr. This is useful when:
/// </para>
/// <list type="bullet">
///     <item>You have existing host configuration code you want to preserve</item>
///     <item>You need fine-grained control over the builder lifecycle</item>
///     <item>You're integrating Needlr into a larger framework</item>
/// </list>
/// <para>
/// Note: <see cref="IHostPlugin"/> plugins are NOT executed by <see cref="UseNeedlrDiscovery"/> because the user
/// controls when <see cref="HostApplicationBuilder.Build"/> is called. If you need IHostPlugin support,
/// call <see cref="RunHostPlugins"/> after building the host.
/// </para>
/// </remarks>
/// <example>
/// Basic usage with user-controlled builder:
/// <code>
/// var builder = Host.CreateApplicationBuilder(args);
/// 
/// // Your existing configuration
/// builder.Services.AddMyCustomServices();
/// 
/// // Add Needlr discovery - runs IHostApplicationBuilderPlugin and IServiceCollectionPlugin
/// builder.UseNeedlrDiscovery();
/// 
/// // More of your configuration
/// builder.Services.AddOtherServices();
/// 
/// var host = builder.Build();
/// 
/// // Optionally run IHostPlugin plugins
/// host.RunHostPlugins();
/// 
/// await host.RunAsync();
/// </code>
/// </example>
public static class HostApplicationBuilderNeedlrExtensions
{
    /// <summary>
    /// Integrates Needlr's automatic service discovery and plugin system into the <see cref="HostApplicationBuilder"/>.
    /// </summary>
    /// <param name="builder">The host application builder to configure.</param>
    /// <param name="syringe">
    /// Optional syringe instance to use for configuration. If not provided, a default syringe is created.
    /// Use this to provide a pre-configured syringe with specific type registrars or filterers.
    /// </param>
    /// <param name="logger">Optional logger for discovery and plugin execution logging.</param>
    /// <returns>The same <see cref="HostApplicationBuilder"/> for method chaining.</returns>
    /// <remarks>
    /// <para>This method performs the following in order:</para>
    /// <list type="number">
    ///     <item>Discovers assemblies using the syringe's assembly provider</item>
    ///     <item>Runs all <see cref="IHostApplicationBuilderPlugin.Configure"/> methods</item>
    ///     <item>Runs all <see cref="IServiceCollectionPlugin.Configure"/> methods</item>
    ///     <item>Registers discovered types to the service collection</item>
    /// </list>
    /// <para>
    /// <see cref="IHostPlugin"/> plugins are NOT executed. If needed, call <see cref="RunHostPlugins"/>
    /// after <see cref="HostApplicationBuilder.Build"/>.
    /// </para>
    /// </remarks>
    /// <example>
    /// With a pre-configured syringe:
    /// <code>
    /// var syringe = new Syringe()
    ///     .UsingReflection()
    ///     .UsingAdditionalAssemblies(typeof(MyPluginAssembly).Assembly);
    /// 
    /// var builder = Host.CreateApplicationBuilder(args);
    /// builder.UseNeedlrDiscovery(syringe);
    /// </code>
    /// </example>
    public static HostApplicationBuilder UseNeedlrDiscovery(
        this HostApplicationBuilder builder,
        Syringe? syringe = null,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        syringe ??= new Syringe();
        logger ??= NullLogger.Instance;

        var typeRegistrar = syringe.GetOrCreateTypeRegistrar();
        var typeFilterer = syringe.GetOrCreateTypeFilterer();
        var pluginFactory = syringe.GetOrCreatePluginFactory();
        var serviceCollectionPopulator = syringe.GetOrCreateServiceCollectionPopulator(typeRegistrar, typeFilterer, pluginFactory);
        var assemblyProvider = syringe.GetOrCreateAssemblyProvider();
        var additionalAssemblies = syringe.GetAdditionalAssemblies();

        var serviceProviderBuilder = syringe.GetOrCreateServiceProviderBuilder(
            serviceCollectionPopulator,
            assemblyProvider,
            additionalAssemblies);

        var candidateAssemblies = serviceProviderBuilder.GetCandidateAssemblies();

        // Run IHostApplicationBuilderPlugin plugins
        RunHostApplicationBuilderPlugins(
            builder,
            pluginFactory,
            candidateAssemblies,
            logger);

        // Register discovered services
        logger.LogInformation("Registering discovered services to service collection...");
        serviceCollectionPopulator.RegisterToServiceCollection(
            builder.Services,
            builder.Configuration,
            candidateAssemblies);
        logger.LogInformation("Registered discovered services to service collection.");

        // Store the service provider builder for post-build plugin execution
        builder.Services.AddSingleton(serviceProviderBuilder);
        builder.Services.AddSingleton(pluginFactory);
        builder.Services.AddSingleton<IReadOnlyList<Assembly>>(candidateAssemblies);

        return builder;
    }

    /// <summary>
    /// Runs <see cref="IHostPlugin"/> plugins on the built host.
    /// Call this after <see cref="HostApplicationBuilder.Build"/> if you need IHostPlugin support.
    /// </summary>
    /// <param name="host">The built host to configure.</param>
    /// <param name="logger">Optional logger for plugin execution logging.</param>
    /// <returns>The same <see cref="IHost"/> for method chaining.</returns>
    /// <remarks>
    /// This method retrieves the plugin factory and candidate assemblies that were stored
    /// during <see cref="UseNeedlrDiscovery"/> and uses them to run IHostPlugin plugins.
    /// </remarks>
    /// <example>
    /// <code>
    /// var builder = Host.CreateApplicationBuilder(args);
    /// builder.UseNeedlrDiscovery();
    /// 
    /// var host = builder.Build();
    /// host.RunHostPlugins(); // Run IHostPlugin plugins
    /// 
    /// await host.RunAsync();
    /// </code>
    /// </example>
    public static IHost RunHostPlugins(
        this IHost host,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(host);

        logger ??= NullLogger.Instance;

        var pluginFactory = host.Services.GetRequiredService<IPluginFactory>();
        var candidateAssemblies = host.Services.GetRequiredService<IReadOnlyList<Assembly>>();

        RegisterHostPlugins(
            host,
            pluginFactory,
            candidateAssemblies,
            logger);

        // Run post-build service collection plugins
        var serviceProviderBuilder = host.Services.GetRequiredService<IServiceProviderBuilder>();
        var configuration = host.Services.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
        serviceProviderBuilder.ConfigurePostBuildServiceCollectionPlugins(
            host.Services,
            configuration);

        return host;
    }

    private static void RunHostApplicationBuilderPlugins(
        HostApplicationBuilder builder,
        IPluginFactory pluginFactory,
        IReadOnlyList<Assembly> candidateAssemblies,
        ILogger logger)
    {
        logger.LogInformation("Configuring IHostApplicationBuilderPlugin plugins...");

        HostApplicationBuilderPluginOptions options = new(
            builder,
            candidateAssemblies,
            logger,
            pluginFactory);

        foreach (var plugin in pluginFactory.CreatePluginsFromAssemblies<IHostApplicationBuilderPlugin>(
            candidateAssemblies))
        {
            logger.LogInformation("Configuring host application builder plugin '{PluginName}'...", plugin.GetType().Name);
            plugin.Configure(options);
        }

        logger.LogInformation("Configured IHostApplicationBuilderPlugin plugins.");
    }

    private static void RegisterHostPlugins(
        IHost host,
        IPluginFactory pluginFactory,
        IReadOnlyList<Assembly> candidateAssemblies,
        ILogger logger)
    {
        logger.LogInformation("Configuring IHostPlugin plugins...");

        HostPluginOptions options = new(
            host,
            candidateAssemblies,
            pluginFactory);

        foreach (var plugin in pluginFactory.CreatePluginsFromAssemblies<IHostPlugin>(
            candidateAssemblies))
        {
            logger.LogInformation("Configuring host plugin '{PluginName}'...", plugin.GetType().Name);
            plugin.Configure(options);
        }

        logger.LogInformation("Configured IHostPlugin plugins.");
    }
}
