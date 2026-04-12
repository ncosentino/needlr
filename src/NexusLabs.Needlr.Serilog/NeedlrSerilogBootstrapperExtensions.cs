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
        return bootstrapper with { ConfigureAction = configure };
    }
}
