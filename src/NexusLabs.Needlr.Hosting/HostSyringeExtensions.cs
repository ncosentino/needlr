using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

using NexusLabs.Needlr.Injection;

namespace NexusLabs.Needlr.Hosting;

/// <summary>
/// Extension methods for configuring <see cref="HostSyringe"/> instances.
/// Provides only host-specific configuration methods.
/// </summary>
/// <remarks>
/// Remember to use base <see cref="Syringe"/> extension methods BEFORE transitioning to host mode.
/// </remarks>
/// <example>
/// Complete host configuration:
/// <code>
/// var host = new Syringe()
///     // Transition to host mode
///     .ForHost()
///     // Configure host-specific options
///     .UsingOptions(() => CreateHostOptions.Default
///         .UsingArgs(args)
///         .UsingApplicationName("My Worker Service")
///         .UsingStartupConsoleLogger())
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
/// 
/// Minimal host:
/// <code>
/// var host = new Syringe()
///     .ForHost()
///     .BuildHost();
/// </code>
/// </example>
public static class HostSyringeExtensions
{
    /// <summary>
    /// Configures the <see cref="HostSyringe"/> to use the specified host options factory.
    /// </summary>
    /// <param name="syringe">The <see cref="HostSyringe"/> to configure.</param>
    /// <param name="optionsFactory">The factory function for creating host options.</param>
    /// <returns>A new configured <see cref="HostSyringe"/> instance.</returns>
    /// <example>
    /// <code>
    /// var hostSyringe = syringe
    ///     .ForHost()
    ///     .UsingOptions(() => CreateHostOptions.Default
    ///         .UsingArgs(args)
    ///         .UsingApplicationName("My App")
    ///         .UsingStartupConsoleLogger());
    /// </code>
    /// </example>
    public static HostSyringe UsingOptions(
        this HostSyringe syringe,
        Func<CreateHostOptions> optionsFactory)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        ArgumentNullException.ThrowIfNull(optionsFactory);

        return syringe with { OptionsFactory = optionsFactory };
    }

    /// <summary>
    /// Configures the <see cref="HostSyringe"/> to use the specified host factory.
    /// </summary>
    /// <param name="syringe">The <see cref="HostSyringe"/> to configure.</param>
    /// <param name="factory">The factory function for creating host factories.</param>
    /// <returns>A new configured <see cref="HostSyringe"/> instance.</returns>
    /// <example>
    /// <code>
    /// var hostSyringe = syringe
    ///     .ForHost()
    ///     .UsingHostFactory((serviceProviderBuilder, serviceCollectionPopulator) => 
    ///     {
    ///         // Custom factory logic here
    ///         return new CustomHostFactory(serviceProviderBuilder, serviceCollectionPopulator);
    ///     });
    /// </code>
    /// </example>
    public static HostSyringe UsingHostFactory(
        this HostSyringe syringe,
        Func<IServiceProviderBuilder, IServiceCollectionPopulator, IHostFactory> factory)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        ArgumentNullException.ThrowIfNull(factory);

        return syringe with { HostFactoryCreator = factory };
    }

    /// <summary>
    /// Configures the <see cref="HostSyringe"/> to use a callback for configuring the <see cref="HostApplicationBuilder"/>.
    /// This allows for custom configuration of the builder, such as modifying the <see cref="ConfigurationBuilder"/>
    /// or adding additional services before the host is built.
    /// </summary>
    /// <param name="syringe">The <see cref="HostSyringe"/> to configure.</param>
    /// <param name="configureCallback">The callback to configure the <see cref="HostApplicationBuilder"/>.</param>
    /// <returns>A new configured <see cref="HostSyringe"/> instance.</returns>
    /// <example>
    /// <code>
    /// var hostSyringe = syringe
    ///     .ForHost()
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
    public static HostSyringe UsingConfigurationCallback(
        this HostSyringe syringe,
        Action<HostApplicationBuilder, CreateHostOptions> configureCallback)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        ArgumentNullException.ThrowIfNull(configureCallback);

        return syringe with { ConfigureCallback = configureCallback };
    }
}
