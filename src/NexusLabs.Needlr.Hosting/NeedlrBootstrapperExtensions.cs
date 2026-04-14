using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace NexusLabs.Needlr.Hosting;

/// <summary>
/// Extension methods for configuring <see cref="NeedlrBootstrapper"/> instances.
/// </summary>
public static class NeedlrBootstrapperExtensions
{
    /// <summary>
    /// Overrides the default console logger factory with a custom <see cref="ILoggerFactory"/>.
    /// </summary>
    /// <param name="bootstrapper">The bootstrapper to configure.</param>
    /// <param name="loggerFactory">The logger factory to use.</param>
    /// <returns>A new <see cref="NeedlrBootstrapper"/> with the factory applied.</returns>
    /// <example>
    /// <code>
    /// await new NeedlrBootstrapper()
    ///     .UsingLoggerFactory(myLoggerFactory)
    ///     .RunAsync(async (ctx, ct) => { /* ... */ });
    /// </code>
    /// </example>
    public static NeedlrBootstrapper UsingLoggerFactory(
        this NeedlrBootstrapper bootstrapper,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(bootstrapper);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        return bootstrapper with { Factory = loggerFactory };
    }

    /// <summary>
    /// Registers an async cleanup callback that runs in the <c>finally</c> block of
    /// <see cref="NeedlrBootstrapper.RunAsync"/>, regardless of whether the application
    /// succeeds or throws.
    /// </summary>
    /// <param name="bootstrapper">The bootstrapper to configure.</param>
    /// <param name="cleanup">The async cleanup delegate (e.g. flushing a log sink).</param>
    /// <returns>A new <see cref="NeedlrBootstrapper"/> with the cleanup registered.</returns>
    /// <example>
    /// <code>
    /// await new NeedlrBootstrapper()
    ///     .WithCleanup(async () => await FlushLogsAsync())
    ///     .RunAsync(async (ctx, ct) => { /* ... */ });
    /// </code>
    /// </example>
    public static NeedlrBootstrapper WithCleanup(
        this NeedlrBootstrapper bootstrapper,
        Func<Task> cleanup)
    {
        ArgumentNullException.ThrowIfNull(bootstrapper);
        ArgumentNullException.ThrowIfNull(cleanup);
        return bootstrapper with { Cleanup = cleanup };
    }

    /// <summary>
    /// Configures the bootstrap-phase <see cref="IConfiguration"/> by adding sources to the
    /// <see cref="IConfigurationBuilder"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The bootstrap configuration is <strong>not</strong> the same <see cref="IConfiguration"/>
    /// that the application's DI container will provide. It exists only for the duration of the
    /// bootstrap callback and is disposed after <see cref="NeedlrBootstrapper.RunAsync"/> completes.
    /// Syringe (and the .NET Generic Host / WebApplication builder) builds its own
    /// <see cref="IConfiguration"/> independently.
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
    /// <returns>A new <see cref="NeedlrBootstrapper"/> with the configuration builder registered.</returns>
    /// <example>
    /// <code>
    /// await new NeedlrBootstrapper()
    ///     .ConfigureBootstrapConfiguration(builder => builder
    ///         .AddJsonFile("appsettings.json", optional: true)
    ///         .AddEnvironmentVariables())
    ///     .RunAsync(async (ctx, ct) =>
    ///     {
    ///         var logDir = ctx.BootstrapConfiguration["Logging:Directory"]
    ///             ?? "logs";
    ///         ctx.Logger.LogInformation("Bootstrap log directory: {Dir}", logDir);
    ///     });
    /// </code>
    /// </example>
    public static NeedlrBootstrapper ConfigureBootstrapConfiguration(
        this NeedlrBootstrapper bootstrapper,
        Action<IConfigurationBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(bootstrapper);
        ArgumentNullException.ThrowIfNull(configure);
        return bootstrapper with { ConfigureBootstrapConfigurationBuilder = configure };
    }
}
