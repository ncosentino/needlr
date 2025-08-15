using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using NexusLabs.Needlr.Injection;

using System.Reflection;

namespace NexusLabs.Needlr.AspNet;

[DoNotAutoRegister]
public sealed class WebApplicationFactory(
    IServiceProviderBuilder _serviceProviderBuilder,
    IServiceCollectionPopulator _serviceCollectionPopulator) :
    IWebApplicationFactory
{
    private readonly PluginFactory _pluginFactory = new();

    /// <inheritdoc />
    public WebApplication Create(
        CreateWebApplicationOptions options,
        Func<WebApplicationBuilder> createWebApplicationBuilderCallback)
    {
        options.Logger.LogInformation("Creating web application builder...");
        var webApplicationBuilder = createWebApplicationBuilderCallback.Invoke();

        var candidateAssemblies = _serviceProviderBuilder.GetCandidateAssemblies();

        ConfigureServices(
            _serviceCollectionPopulator,
            webApplicationBuilder,
            _pluginFactory,
            options.Logger,
            candidateAssemblies,
            options.PostPluginRegistrationCallbacks);

        options.Logger.LogInformation("Building web application...");
        var webApplication = webApplicationBuilder.Build();

        ConfigureWebApplication(
            _serviceProviderBuilder,
            _pluginFactory,
            webApplication,
            options.Logger,
            candidateAssemblies);

        options.Logger.LogInformation("Web application created successfully.");
        return webApplication;
    }

    private static void ConfigureWebApplication(
        IServiceProviderBuilder serviceProviderBuilder,
        IPluginFactory pluginFactory,
        WebApplication webApplication,
        ILogger logger,
        IReadOnlyList<Assembly> assembliesToLoadFrom)
    {
        ArgumentNullException.ThrowIfNull(serviceProviderBuilder);
        ArgumentNullException.ThrowIfNull(pluginFactory);
        ArgumentNullException.ThrowIfNull(webApplication);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(assembliesToLoadFrom);

        logger.LogInformation("Configuring web application...");

        RegisterWebApplicationPlugins(
            webApplication,
            pluginFactory,
            assembliesToLoadFrom,
            logger);

        serviceProviderBuilder.ConfigurePostBuildServiceCollectionPlugins(
            webApplication.Services,
            webApplication.Configuration);

        logger.LogInformation("Web application configured successfully.");
    }

    private static void RegisterWebApplicationPlugins(
        WebApplication webApplication,
        IPluginFactory pluginFactory,
        IReadOnlyList<Assembly> assembliesToLoadFrom,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(webApplication);
        ArgumentNullException.ThrowIfNull(pluginFactory);
        ArgumentNullException.ThrowIfNull(assembliesToLoadFrom);
        ArgumentNullException.ThrowIfNull(logger);

        logger.LogInformation("Configuring plugins for the web application...");

        WebApplicationPluginOptions options = new(
            webApplication,
            assembliesToLoadFrom);
        foreach (var plugin in pluginFactory.CreatePluginsFromAssemblies<IWebApplicationPlugin>(
            assembliesToLoadFrom))
        {
            logger.LogInformation("Configuring web application plugin '{PluginName}'...", plugin.GetType().Name);
            plugin.Configure(options);
        }

        logger.LogInformation("Configured plugins for the web application.");
    }

    private static void RegisterWebApplicationBuilderPlugins(
        WebApplicationBuilder builder,
        IPluginFactory pluginFactory,
        IReadOnlyList<Assembly> assembliesToLoadFrom,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(pluginFactory);
        ArgumentNullException.ThrowIfNull(assembliesToLoadFrom);
        ArgumentNullException.ThrowIfNull(logger);

        logger.LogInformation("Configuring plugins for the web application builder...");

        WebApplicationBuilderPluginOptions options = new(
            builder,
            assembliesToLoadFrom,
            logger);
        foreach (var plugin in pluginFactory.CreatePluginsFromAssemblies<IWebApplicationBuilderPlugin>(
            assembliesToLoadFrom))
        {
            logger.LogInformation("Configuring web application builder plugin '{PluginName}'...", plugin.GetType().Name);
            plugin.Configure(options);
        }

        logger.LogInformation("Configured plugins for the web application builder.");
    }

    private static void ConfigureServices(
        IServiceCollectionPopulator serviceCollectionPopulator,
        WebApplicationBuilder builder,
        IPluginFactory pluginFactory,
        ILogger logger,
        IReadOnlyList<Assembly> assembliesToLoadFrom,
        IReadOnlyList<Action<IServiceCollection>> postPluginRegistrationCallbacks)
    {
        ArgumentNullException.ThrowIfNull(serviceCollectionPopulator);
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(pluginFactory);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(assembliesToLoadFrom);
        ArgumentNullException.ThrowIfNull(postPluginRegistrationCallbacks);

        logger.LogInformation("Configuring web application services...");

        RegisterWebApplicationBuilderPlugins(
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

        // Execute post-plugin registration callbacks
        foreach (var callback in postPluginRegistrationCallbacks)
        {
            logger.LogInformation("Executing post-plugin registration callback...");
            callback.Invoke(builder.Services);
        }

        logger.LogInformation("Configured web application services successfully.");
    }
}