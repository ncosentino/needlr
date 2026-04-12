using Microsoft.Extensions.Logging;

namespace NexusLabs.Needlr.Hosting;

/// <summary>
/// Wraps an application entry point with bootstrap lifecycle management: a pre-DI logger,
/// top-level exception handling, and guaranteed cleanup.
/// </summary>
/// <remarks>
/// <para>
/// By default a console logger is created automatically. Override with
/// <see cref="NeedlrBootstrapperExtensions.UsingLoggerFactory"/> to supply your own factory
/// (e.g. a Serilog two-stage init factory).
/// </para>
/// <para>
/// Unhandled exceptions from the callback are caught, logged at <c>Critical</c>, and then
/// swallowed so the process exits cleanly after cleanup.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// await new NeedlrBootstrapper().RunAsync(async (ctx, ct) =>
/// {
///     var host = new Syringe()
///         .UsingSourceGen()
///         .ForHost()
///         .UsingOptions(() => CreateHostOptions.Default.UsingCurrentProcessArgs())
///         .BuildHost();
///
///     await host.RunAsync(ct);
/// });
/// </code>
/// </example>
[DoNotAutoRegister]
public sealed record NeedlrBootstrapper
{
    internal ILoggerFactory? Factory { get; init; }
    internal Func<Task>? Cleanup { get; init; }

    /// <summary>
    /// Runs the application entry point with full bootstrap lifecycle management.
    /// </summary>
    /// <param name="runAsync">
    /// The application callback. Receives a <see cref="NeedlrBootstrapContext"/> containing
    /// the bootstrap logger, and the <see cref="CancellationToken"/> passed to this method.
    /// </param>
    /// <param name="cancellationToken">
    /// Optional cancellation token forwarded to the callback.
    /// </param>
    /// <returns>A <see cref="Task"/> that completes when the application exits.</returns>
    /// <example>
    /// <code>
    /// await new NeedlrBootstrapper().RunAsync(async (ctx, ct) =>
    /// {
    ///     ctx.Logger.LogInformation("Application starting...");
    ///     await RunMyAppAsync(ct);
    /// });
    /// </code>
    /// </example>
    public async Task RunAsync(
        Func<NeedlrBootstrapContext, CancellationToken, Task> runAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(runAsync);

        var ownsFactory = Factory is null;
        var loggerFactory = Factory ?? LoggerFactory.Create(b => b.AddConsole());
        var logger = loggerFactory.CreateLogger("Startup");

        try
        {
            await runAsync(
                new NeedlrBootstrapContext { Logger = logger },
                cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Application terminated unexpectedly.");
        }
        finally
        {
            if (Cleanup is not null)
            {
                await Cleanup().ConfigureAwait(false);
            }

            if (ownsFactory)
            {
                loggerFactory.Dispose();
            }
        }
    }
}
