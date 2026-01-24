using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using NexusLabs.Needlr.Injection;

namespace NexusLabs.Needlr.AspNet;

/// <summary>
/// Provides a fluent API for configuring and building web applications using Needlr.
/// Wraps a ConfiguredSyringe with additional web application functionality.
/// </summary>
/// <example>
/// Creating and configuring a WebApplicationSyringe:
/// <code>
/// // Method 1: Transition from ConfiguredSyringe
/// var webAppSyringe = new Syringe()
///     .UsingReflection()
///     .ForWebApplication();
/// 
/// // Build and run the web application
/// var webApp = webAppSyringe
///     .UsingOptions(() => CreateWebApplicationOptions.Default.UsingCliArgs(args))
///     .UsingConfigurationCallback((builder, options) => 
///     {
///         // Configure the WebApplicationBuilder
///         builder.Configuration.AddJsonFile("custom-settings.json", optional: true);
///         builder.Services.AddSingleton&lt;IMyCustomService, MyCustomService&gt;();
///     })
///     .BuildWebApplication();
/// 
/// await webApp.RunAsync();
/// </code>
/// </example>
[DoNotAutoRegister]
public sealed record WebApplicationSyringe
{
    internal ConfiguredSyringe BaseSyringe { get; init; }
    internal Func<CreateWebApplicationOptions>? OptionsFactory { get; init; }
    internal Func<IServiceProviderBuilder, IServiceCollectionPopulator, IWebApplicationFactory>? WebApplicationFactoryCreator { get; init; }
    internal Action<WebApplicationBuilder, CreateWebApplicationOptions>? ConfigureCallback { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebApplicationSyringe"/> class.
    /// </summary>
    /// <param name="baseSyringe">The configured syringe to wrap.</param>
    /// <example>
    /// <code>
    /// var configuredSyringe = new Syringe().UsingReflection();
    /// 
    /// var webAppSyringe = new WebApplicationSyringe(configuredSyringe);
    /// </code>
    /// </example>
    public WebApplicationSyringe(ConfiguredSyringe baseSyringe)
    {
        ArgumentNullException.ThrowIfNull(baseSyringe);
        BaseSyringe = baseSyringe;
    }

    /// <summary>
    /// Builds a web application with the configured settings.
    /// </summary>
    /// <returns>The configured <see cref="WebApplication"/>.</returns>
    /// <example>
    /// <code>
    /// var webApplication = new Syringe()
    ///     .UsingReflection()
    ///     .UsingScrutorTypeRegistrar()
    ///     .ForWebApplication()
    ///     .UsingOptions(() => CreateWebApplicationOptions.Default
    ///         .UsingCliArgs(args)
    ///         .UsingApplicationName("My Web App"))
    ///     .UsingConfigurationCallback((builder, options) =>
    ///     {
    ///         // Add custom configuration sources
    ///         builder.Configuration.AddJsonFile("appsettings.local.json", optional: true);
    ///         
    ///         // Register additional services
    ///         builder.Services.AddSingleton&lt;ICustomService, CustomService&gt;();
    ///     })
    ///     .BuildWebApplication();
    /// 
    /// // Configure middleware and endpoints
    /// webApplication.MapGet("/", () => "Hello World!");
    /// 
    /// await webApplication.RunAsync();
    /// </code>
    /// </example>
    public WebApplication BuildWebApplication()
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

        var webApplicationFactory = GetOrCreateWebApplicationFactory(serviceProviderBuilder, serviceCollectionPopulator, pluginFactory);
        var options = GetOrCreateOptions();
        if (callbacks.Count > 0)
        {
            options = options.UsingPostPluginRegistrationCallbacks(callbacks);
        }

        return webApplicationFactory.Create(
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
    ///     .UsingReflection()
    ///     .UsingScrutorTypeRegistrar()
    ///     .ForWebApplication()
    ///     .BuildServiceProvider(config);
    /// 
    /// var myService = serviceProvider.GetRequiredService&lt;IMyService&gt;();
    /// </code>
    /// </example>
    public IServiceProvider BuildServiceProvider(IConfiguration config)
    {
        return BaseSyringe.BuildServiceProvider(config);
    }

    private IWebApplicationFactory GetOrCreateWebApplicationFactory(
        IServiceProviderBuilder serviceProviderBuilder, 
        IServiceCollectionPopulator serviceCollectionPopulator,
        IPluginFactory pluginFactory)
    {
        return WebApplicationFactoryCreator?.Invoke(serviceProviderBuilder, serviceCollectionPopulator)
            ?? new WebApplicationFactory(serviceProviderBuilder, serviceCollectionPopulator, pluginFactory);
    }

    private CreateWebApplicationOptions GetOrCreateOptions()
    {
        return OptionsFactory?.Invoke() ?? CreateWebApplicationOptions.Default;
    }
}