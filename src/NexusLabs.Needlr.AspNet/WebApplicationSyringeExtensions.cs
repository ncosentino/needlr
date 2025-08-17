using Microsoft.AspNetCore.Builder;
using NexusLabs.Needlr.Injection;

namespace NexusLabs.Needlr.AspNet;

/// <summary>
/// Extension methods for configuring <see cref="WebApplicationSyringe"/> instances.
/// Provides only web application specific configuration methods.
/// </summary>
/// <remarks>
/// Remember to use base <see cref="Syringe"/> extension methods BEFORE calling 
/// <seealso cref="SyringeAspNetExtensions.ForWebApplication(Syringe)"/>.
/// </remarks>
/// <example>
/// Complete web application configuration:
/// <code>
/// var webApplication = new Syringe()
///     // Configure base Syringe first
///     .UsingScrutorTypeRegistrar()
///     .UsingDefaultTypeFilterer()
///     .UsingAssemblyProvider(builder => builder
///         .MatchingAssemblies(x => x.Contains("MyApp"))
///         .Build())
///     // Transition to web application mode
///     .ForWebApplication()
///     // Configure web-specific options
///     .UsingOptions(() => CreateWebApplicationOptions.Default
///         .UsingCliArgs(args)
///         .UsingApplicationName("My Web Application")
///         .UsingStartupConsoleLogger())
///     .UsingWebApplicationFactory((serviceProviderBuilder, serviceCollectionPopulator) => 
///         new WebApplicationFactory(serviceProviderBuilder, serviceCollectionPopulator))
///     .UsingConfigurationCallback((builder, options) =>
///     {
///         // Configure the WebApplicationBuilder
///         builder.Configuration.AddJsonFile("custom-settings.json", optional: true);
///         builder.Services.AddSingleton&lt;IMyCustomService, MyCustomService&gt;();
///     })
///     .BuildWebApplication();
/// 
/// await webApplication.RunAsync();
/// </code>
/// 
/// Minimal web application:
/// <code>
/// var webApp = new Syringe()
///     .UsingScrutorTypeRegistrar()
///     .ForWebApplication()
///     .BuildWebApplication();
/// </code>
/// </example>
public static class WebApplicationSyringeExtensions
{
    /// <summary>
    /// Configures the web application syringe to use the specified web application options factory.
    /// </summary>
    /// <param name="syringe">The web application syringe to configure.</param>
    /// <param name="optionsFactory">The factory function for creating web application options.</param>
    /// <returns>A new configured web application syringe instance.</returns>
    /// <example>
    /// <code>
    /// var webAppSyringe = syringe
    ///     .ForWebApplication()
    ///     .UsingOptions(() => CreateWebApplicationOptions.Default
    ///         .UsingCliArgs(args)
    ///         .UsingApplicationName("My App")
    ///         .UsingStartupConsoleLogger());
    /// </code>
    /// </example>
    public static WebApplicationSyringe UsingOptions(
        this WebApplicationSyringe syringe, 
        Func<CreateWebApplicationOptions> optionsFactory)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        ArgumentNullException.ThrowIfNull(optionsFactory);

        return syringe with { OptionsFactory = optionsFactory };
    }

    /// <summary>
    /// Configures the web application syringe to use the specified web application factory.
    /// </summary>
    /// <param name="syringe">The web application syringe to configure.</param>
    /// <param name="factory">The factory function for creating web application factories.</param>
    /// <returns>A new configured web application syringe instance.</returns>
    /// <example>
    /// <code>
    /// var webAppSyringe = syringe
    ///     .ForWebApplication()
    ///     .UsingWebApplicationFactory((serviceProviderBuilder, serviceCollectionPopulator) => 
    ///     {
    ///         // Custom factory logic here
    ///         return new CustomWebApplicationFactory(serviceProviderBuilder, serviceCollectionPopulator);
    ///     });
    /// </code>
    /// </example>
    public static WebApplicationSyringe UsingWebApplicationFactory(
        this WebApplicationSyringe syringe, 
        Func<IServiceProviderBuilder, IServiceCollectionPopulator, IWebApplicationFactory> factory)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        ArgumentNullException.ThrowIfNull(factory);

        return syringe with { WebApplicationFactoryCreator = factory };
    }

    /// <summary>
    /// Configures the web application syringe to use a callback for configuring the WebApplicationBuilder.
    /// This allows for custom configuration of the builder, such as modifying the ConfigurationBuilder
    /// or adding additional services before the web application is built.
    /// </summary>
    /// <param name="syringe">The web application syringe to configure.</param>
    /// <param name="configureCallback">The callback to configure the WebApplicationBuilder.</param>
    /// <returns>A new configured web application syringe instance.</returns>
    /// <example>
    /// <code>
    /// var webAppSyringe = syringe
    ///     .ForWebApplication()
    ///     .UsingConfigurationCallback((builder, options) =>
    ///     {
    ///         // Add custom configuration sources
    ///         builder.Configuration.AddJsonFile("appsettings.local.json", optional: true);
    ///         builder.Configuration.AddEnvironmentVariables("MYAPP_");
    ///         
    ///         // Register additional services that need to be available before plugin registration
    ///         builder.Services.AddSingleton&lt;ICustomConfigurationService, CustomConfigurationService&gt;();
    ///         
    ///         // Configure logging
    ///         builder.Services.AddLogging(logging =>
    ///         {
    ///             logging.AddConsole();
    ///             logging.SetMinimumLevel(LogLevel.Debug);
    ///         });
    ///     });
    /// </code>
    /// </example>
    public static WebApplicationSyringe UsingConfigurationCallback(
        this WebApplicationSyringe syringe,
        Action<WebApplicationBuilder, CreateWebApplicationOptions> configureCallback)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        ArgumentNullException.ThrowIfNull(configureCallback);

        return syringe with { ConfigureCallback = configureCallback };
    }
}