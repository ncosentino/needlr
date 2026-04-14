using Microsoft.Extensions.Configuration;

using Serilog;

namespace NexusLabs.Needlr.Serilog;

/// <summary>
/// Extension methods for configuring <see cref="NeedlrSerilogBootstrapper"/> instances.
/// </summary>
public static class NeedlrSerilogBootstrapperExtensions
{
    /// <summary>
    /// Applies a custom <see cref="LoggerConfiguration"/> to the bootstrapper.
    /// If not called, a default console sink is used.
    /// </summary>
    /// <param name="bootstrapper">The bootstrapper to configure.</param>
    /// <param name="configure">A delegate that configures the <see cref="LoggerConfiguration"/>.</param>
    /// <returns>A new <see cref="NeedlrSerilogBootstrapper"/> with the configuration applied.</returns>
    /// <example>
    /// <code>
    /// await new NeedlrSerilogBootstrapper()
    ///     .Configure(cfg => cfg
    ///         .MinimumLevel.Debug()
    ///         .WriteTo.Console())
    ///     .RunAsync(async (ctx, ct) => { /* ... */ });
    /// </code>
    /// </example>
    public static NeedlrSerilogBootstrapper Configure(
        this NeedlrSerilogBootstrapper bootstrapper,
        Action<LoggerConfiguration> configure)
    {
        ArgumentNullException.ThrowIfNull(bootstrapper);
        ArgumentNullException.ThrowIfNull(configure);
        return bootstrapper with
        {
            ConfigureAction = configure,
            ConfigureWithConfigAction = null,
        };
    }

    /// <summary>
    /// Applies a custom <see cref="LoggerConfiguration"/> that can read from the
    /// bootstrap-phase <see cref="IConfiguration"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <paramref name="configure"/> delegate receives the bootstrap
    /// <see cref="IConfiguration"/> as its second parameter. This is the same configuration
    /// built by <see cref="ConfigureBootstrapConfiguration"/> and is <strong>not</strong>
    /// the application's DI-provided <see cref="IConfiguration"/>.
    /// </para>
    /// <para>
    /// A common pattern is to use <c>cfg.ReadFrom.Configuration(bootstrapConfiguration)</c>
    /// to load Serilog settings from a JSON file during bootstrap, while the real application
    /// logger is configured separately by the DI container.
    /// </para>
    /// <para>
    /// If called multiple times, only the last call takes effect. This overload replaces
    /// any prior <see cref="Configure(NeedlrSerilogBootstrapper, Action{LoggerConfiguration})"/>
    /// call and vice versa.
    /// </para>
    /// </remarks>
    /// <param name="bootstrapper">The bootstrapper to configure.</param>
    /// <param name="configure">
    /// A delegate that configures <see cref="LoggerConfiguration"/> using the bootstrap
    /// <see cref="IConfiguration"/>. The second parameter is named
    /// <c>bootstrapConfiguration</c> to emphasize it is not the application's configuration.
    /// </param>
    /// <returns>A new <see cref="NeedlrSerilogBootstrapper"/> with the configuration applied.</returns>
    /// <example>
    /// <code>
    /// await new NeedlrSerilogBootstrapper()
    ///     .ConfigureBootstrapConfiguration(builder => builder
    ///         .AddJsonFile("appsettings.json", optional: true))
    ///     .Configure((cfg, bootstrapConfiguration) => cfg
    ///         .ReadFrom.Configuration(bootstrapConfiguration)
    ///         .WriteTo.Console())
    ///     .RunAsync(async (ctx, ct) => { /* ... */ });
    /// </code>
    /// </example>
    public static NeedlrSerilogBootstrapper Configure(
        this NeedlrSerilogBootstrapper bootstrapper,
        Action<LoggerConfiguration, IConfiguration> configure)
    {
        ArgumentNullException.ThrowIfNull(bootstrapper);
        ArgumentNullException.ThrowIfNull(configure);
        return bootstrapper with
        {
            ConfigureWithConfigAction = configure,
            ConfigureAction = null,
        };
    }

    /// <summary>
    /// Configures the bootstrap-phase <see cref="IConfiguration"/> by adding sources to the
    /// <see cref="IConfigurationBuilder"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The bootstrap configuration is <strong>not</strong> the same <see cref="IConfiguration"/>
    /// that the application's DI container will provide. It exists only for the duration of the
    /// bootstrap callback and is disposed after the bootstrapper completes. Syringe (and the .NET
    /// Generic Host / WebApplication builder) builds its own <see cref="IConfiguration"/>
    /// independently.
    /// </para>
    /// <para>
    /// By default the bootstrap configuration is <strong>empty</strong>. Call this method to add
    /// JSON files, environment variables, in-memory collections, or any other
    /// <see cref="IConfigurationSource"/> needed during the bootstrap phase.
    /// </para>
    /// <para>
    /// If called multiple times, only the last call takes effect.
    /// </para>
    /// </remarks>
    /// <param name="bootstrapper">The bootstrapper to configure.</param>
    /// <param name="configure">
    /// A delegate that adds configuration sources to the <see cref="IConfigurationBuilder"/>.
    /// </param>
    /// <returns>A new <see cref="NeedlrSerilogBootstrapper"/> with the configuration builder registered.</returns>
    /// <example>
    /// <code>
    /// await new NeedlrSerilogBootstrapper()
    ///     .ConfigureBootstrapConfiguration(builder => builder
    ///         .AddJsonFile("appsettings.json", optional: true)
    ///         .AddEnvironmentVariables())
    ///     .RunAsync(async (ctx, ct) =>
    ///     {
    ///         var logDir = ctx.BootstrapConfiguration["Logging:Directory"];
    ///     });
    /// </code>
    /// </example>
    public static NeedlrSerilogBootstrapper ConfigureBootstrapConfiguration(
        this NeedlrSerilogBootstrapper bootstrapper,
        Action<IConfigurationBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(bootstrapper);
        ArgumentNullException.ThrowIfNull(configure);
        return bootstrapper with { ConfigureBootstrapConfigurationBuilder = configure };
    }
}
