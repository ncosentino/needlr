using NexusLabs.Needlr.Hosting;

using Serilog;
using Serilog.Extensions.Logging;

namespace NexusLabs.Needlr.Serilog;

/// <summary>
/// Wraps an application entry point with Serilog-specific bootstrap lifecycle management:
/// two-stage initialization, a pre-DI Serilog logger, top-level exception handling,
/// and automatic log flushing on shutdown.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="NeedlrSerilogBootstrapper"/> composes <see cref="NeedlrBootstrapper"/> internally.
/// All lifecycle behaviour (exception catching, cleanup, logger factory ownership) is delegated
/// to <see cref="NeedlrBootstrapper"/> — this type only adds Serilog-specific wiring:
/// setting <c>Log.Logger</c> before the callback runs and flushing it in <c>finally</c>.
/// </para>
/// <para>
/// By default a console sink is configured. Override with
/// <see cref="NeedlrSerilogBootstrapperExtensions.Configure"/> to apply your own
/// <see cref="LoggerConfiguration"/>.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// await new NeedlrSerilogBootstrapper()
///     .Configure(cfg => cfg
///         .MinimumLevel.Debug()
///         .WriteTo.Console())
///     .RunAsync(async (ctx, ct) =>
///     {
///         var webApp = new Syringe()
///             .UsingSourceGen()
///             .ForWebApplication()
///             .UsingOptions(() => CreateWebApplicationOptions.Default
///                 .UsingCurrentProcessCliArgs()
///                 .UsingLogger(ctx.Logger))
///             .BuildWebApplication();
///
///         await webApp.RunAsync(ct);
///     });
/// </code>
/// </example>
[DoNotAutoRegister]
public sealed record NeedlrSerilogBootstrapper
{
    internal Action<LoggerConfiguration>? ConfigureAction { get; init; }

    /// <summary>
    /// Runs the application entry point with Serilog bootstrap lifecycle management.
    /// </summary>
    /// <param name="runAsync">
    /// The application callback. Receives a <see cref="NeedlrBootstrapContext"/> containing
    /// a bootstrap logger backed by the configured Serilog pipeline.
    /// </param>
    /// <param name="cancellationToken">
    /// Optional cancellation token forwarded to the callback.
    /// </param>
    /// <returns>A <see cref="Task"/> that completes when the application exits.</returns>
    public async Task RunAsync(
        Func<NeedlrBootstrapContext, CancellationToken, Task> runAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(runAsync);

        var configuration = new LoggerConfiguration();
        if (ConfigureAction is not null)
        {
            ConfigureAction(configuration);
        }
        else
        {
            configuration.WriteTo.Console();
        }

        Log.Logger = configuration.CreateLogger();

        var loggerFactory = new SerilogLoggerFactory(dispose: false);
        await new NeedlrBootstrapper()
            .UsingLoggerFactory(loggerFactory)
            .WithCleanup(() => Log.CloseAndFlushAsync().AsTask())
            .RunAsync(runAsync, cancellationToken)
            .ConfigureAwait(false);
    }
}
