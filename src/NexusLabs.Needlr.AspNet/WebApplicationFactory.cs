using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NexusLabs.Needlr.Injection;

using System.Reflection;

namespace NexusLabs.Needlr.AspNet;

[DoNotAutoRegister]
public sealed class WebApplicationFactory(
    IServiceProviderBuilder _serviceProviderBuilder,
    IServiceCollectionPopulator _serviceCollectionPopulator) : IWebApplicationFactory
{
    private readonly PluginFactory _pluginFactory = new();

    public WebApplication Create(
        CreateWebApplicationOptions options,
        Func<WebApplicationBuilder> createWebApplicationBuilderCallback)
    {
        options.Logger.LogInformation("Creating web application builder...");
        var webApplicationBuilder = createWebApplicationBuilderCallback.Invoke();

        ConfigureServices(
            _serviceCollectionPopulator,
            webApplicationBuilder,
            _pluginFactory,
            options);

        options.Logger.LogInformation("Building web application...");
        var webApplication = webApplicationBuilder.Build();

        ConfigureWebApplication(
            _serviceProviderBuilder,
            _pluginFactory,
            webApplication,
            options);

        options.Logger.LogInformation("Web application created successfully.");
        return webApplication;
    }

    private static void ConfigureWebApplication(
        IServiceProviderBuilder serviceProviderBuilder,
        IPluginFactory pluginFactory,
        WebApplication webApplication,
        CreateWebApplicationOptions options)
    {
        ArgumentNullException.ThrowIfNull(serviceProviderBuilder);
        ArgumentNullException.ThrowIfNull(pluginFactory);
        ArgumentNullException.ThrowIfNull(webApplication);
        ArgumentNullException.ThrowIfNull(options);

        options.Logger.LogInformation("Configuring web application...");

        RegisterWebApplicationPlugins(
            webApplication,
            pluginFactory,
            options.AssembliesToLoadFrom,
            options.Logger);

        serviceProviderBuilder.ConfigurePostBuildServiceCollectionPlugins(
            webApplication.Services,
            webApplication.Configuration);

        options.Logger.LogInformation("Web application configured successfully.");
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
        CreateWebApplicationOptions options)
    {
        ArgumentNullException.ThrowIfNull(serviceCollectionPopulator);
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(pluginFactory);
        ArgumentNullException.ThrowIfNull(options);

        options.Logger.LogInformation("Configuring web application services...");

        RegisterWebApplicationBuilderPlugins(
            builder,
            pluginFactory,
            options.AssembliesToLoadFrom,
            options.Logger);

        options.Logger.LogInformation("Registering services to service collection...");
        serviceCollectionPopulator.RegisterToServiceCollection(
            builder.Services,
            builder.Configuration);
        options.Logger.LogInformation("Registered services to service collection.");

        options.Logger.LogInformation("Configured web application services successfully.");
    }
}