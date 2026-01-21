using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using NexusLabs.Needlr.Injection;

using System.Reflection;

namespace NexusLabs.Needlr.Hosting;

/// <summary>
/// Factory for creating <see cref="IHost"/> instances with Needlr configuration.
/// </summary>
[DoNotAutoRegister]
public sealed class HostFactory(
    IServiceProviderBuilder _serviceProviderBuilder,
    IServiceCollectionPopulator _serviceCollectionPopulator,
    IPluginFactory _pluginFactory) :
    IHostFactory
{
    /// <inheritdoc />
    public IHost Create(
        CreateHostOptions options,
        Func<HostApplicationBuilder> createHostApplicationBuilderCallback)
    {
        options.Logger.LogInformation("Creating host application builder...");
        var hostApplicationBuilder = createHostApplicationBuilderCallback.Invoke();

        var candidateAssemblies = _serviceProviderBuilder.GetCandidateAssemblies();

        ConfigureServices(
            _serviceCollectionPopulator,
            hostApplicationBuilder,
            _pluginFactory,
            options.Logger,
            candidateAssemblies,
            options.PrePluginRegistrationCallbacks,
            options.PostPluginRegistrationCallbacks);

        options.Logger.LogInformation("Building host...");
        var host = hostApplicationBuilder.Build();

        ConfigureHost(
            _serviceProviderBuilder,
            _pluginFactory,
            host,
            options.Logger,
            candidateAssemblies);

        options.Logger.LogInformation("Host created successfully.");
        return host;
    }

    private static void ConfigureHost(
        IServiceProviderBuilder serviceProviderBuilder,
        IPluginFactory pluginFactory,
        IHost host,
        ILogger logger,
        IReadOnlyList<Assembly> assembliesToLoadFrom)
    {
        ArgumentNullException.ThrowIfNull(serviceProviderBuilder);
        ArgumentNullException.ThrowIfNull(pluginFactory);
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(assembliesToLoadFrom);

        logger.LogInformation("Configuring host...");

        RegisterHostPlugins(
            host,
            pluginFactory,
            assembliesToLoadFrom,
            logger);

        var configuration = host.Services.GetRequiredService<IConfiguration>();
        serviceProviderBuilder.ConfigurePostBuildServiceCollectionPlugins(
            host.Services,
            configuration);

        logger.LogInformation("Host configured successfully.");
    }

    private static void RegisterHostPlugins(
        IHost host,
        IPluginFactory pluginFactory,
        IReadOnlyList<Assembly> assembliesToLoadFrom,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(pluginFactory);
        ArgumentNullException.ThrowIfNull(assembliesToLoadFrom);
        ArgumentNullException.ThrowIfNull(logger);

        logger.LogInformation("Configuring plugins for the host...");

        HostPluginOptions options = new(
            host,
            assembliesToLoadFrom,
            pluginFactory);
        foreach (var plugin in pluginFactory.CreatePluginsFromAssemblies<IHostPlugin>(
            assembliesToLoadFrom))
        {
            logger.LogInformation("Configuring host plugin '{PluginName}'...", plugin.GetType().Name);
            plugin.Configure(options);
        }

        logger.LogInformation("Configured plugins for the host.");
    }

    private static void RegisterHostApplicationBuilderPlugins(
        HostApplicationBuilder builder,
        IPluginFactory pluginFactory,
        IReadOnlyList<Assembly> assembliesToLoadFrom,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(pluginFactory);
        ArgumentNullException.ThrowIfNull(assembliesToLoadFrom);
        ArgumentNullException.ThrowIfNull(logger);

        logger.LogInformation("Configuring plugins for the host application builder...");

        HostApplicationBuilderPluginOptions options = new(
            builder,
            assembliesToLoadFrom,
            logger,
            pluginFactory);
        foreach (var plugin in pluginFactory.CreatePluginsFromAssemblies<IHostApplicationBuilderPlugin>(
            assembliesToLoadFrom))
        {
            logger.LogInformation("Configuring host application builder plugin '{PluginName}'...", plugin.GetType().Name);
            plugin.Configure(options);
        }

        logger.LogInformation("Configured plugins for the host application builder.");
    }

    private static void ConfigureServices(
        IServiceCollectionPopulator serviceCollectionPopulator,
        HostApplicationBuilder builder,
        IPluginFactory pluginFactory,
        ILogger logger,
        IReadOnlyList<Assembly> assembliesToLoadFrom,
        IReadOnlyList<Action<IServiceCollection>> prePluginRegistrationCallbacks,
        IReadOnlyList<Action<IServiceCollection>> postPluginRegistrationCallbacks)
    {
        ArgumentNullException.ThrowIfNull(serviceCollectionPopulator);
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(pluginFactory);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(assembliesToLoadFrom);
        ArgumentNullException.ThrowIfNull(prePluginRegistrationCallbacks);
        ArgumentNullException.ThrowIfNull(postPluginRegistrationCallbacks);

        logger.LogInformation("Configuring host services...");

        foreach (var callback in prePluginRegistrationCallbacks)
        {
            logger.LogInformation("Executing pre-plugin registration callback...");
            callback.Invoke(builder.Services);
        }

        RegisterHostApplicationBuilderPlugins(
            builder,
            pluginFactory,
            assembliesToLoadFrom,
            logger);

        logger.LogInformation("Registering services to service collection...");
        serviceCollectionPopulator.RegisterToServiceCollection(
            builder.Services,
            builder.Configuration,
            assembliesToLoadFrom);
        logger.LogInformation("Registered services to service collection.");

        foreach (var callback in postPluginRegistrationCallbacks)
        {
            logger.LogInformation("Executing post-plugin registration callback...");
            callback.Invoke(builder.Services);
        }

        logger.LogInformation("Configured host services successfully.");
    }
}
