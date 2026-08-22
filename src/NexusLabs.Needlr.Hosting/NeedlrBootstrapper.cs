using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace NexusLabs.Needlr.Hosting;

/// <summary>
/// Wraps an application entry point with bootstrap lifecycle management: a pre-DI logger,
/// a pre-DI <see cref="IConfiguration"/>, top-level exception handling, and guaranteed cleanup.
/// </summary>
/// <remarks>
/// <para>
/// By default a console logger and an <strong>empty</strong> <see cref="IConfiguration"/> are
/// created automatically. Override with
/// <see cref="NeedlrBootstrapperExtensions.UsingLoggerFactory"/> to supply your own factory
/// (e.g. a Serilog two-stage init factory), and
/// <see cref="NeedlrBootstrapperExtensions.ConfigureBootstrapConfiguration"/> to add
/// configuration sources needed during the bootstrap phase.
/// </para>
/// <para>
/// The bootstrap configuration is <strong>not</strong> the same <see cref="IConfiguration"/>
/// that the application's DI container will provide. They are independent instances.
/// See <see cref="NeedlrBootstrapContext.BootstrapConfiguration"/> for details.
/// </para>
/// <para>
/// Unexpected exceptions from the callback are logged at <c>Critical</c> and rethrown
/// after cleanup so a top-level caller produces a nonzero process exit code. Cooperative
/// cancellation completes normally without a critical log.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// await new NeedlrBootstrapper()
///     .ConfigureBootstrapConfiguration(builder => builder
///         .AddJsonFile("appsettings.json", optional: true)
///         .AddEnvironmentVariables())
///     .RunAsync(async (ctx, ct) =>
///     {
///         var host = new Syringe()
///             .UsingSourceGen()
///             .ForHost()
///             .UsingOptions(() => CreateHostOptions.Default.UsingCurrentProcessArgs())
///             .BuildHost();
///
///         await host.RunAsync(ct);
///     });
/// </code>
/// </example>
[DoNotAutoRegister]
public sealed record NeedlrBootstrapper
{
    internal ILoggerFactory? Factory { get; init; }
    internal Func<Task>? Cleanup { get; init; }
    internal Action<IConfigurationBuilder>? ConfigureBootstrapConfigurationBuilder { get; init; }

    /// <summary>
    /// Runs the application entry point with full bootstrap lifecycle management.
    /// </summary>
    /// <param name="runAsync">
    /// The application callback. Receives a <see cref="NeedlrBootstrapContext"/> containing
    /// the bootstrap logger and bootstrap configuration, and the <see cref="CancellationToken"/>
    /// passed to this method.
    /// </param>
    /// <param name="cancellationToken">
    /// Optional cancellation token forwarded to the callback.
    /// </param>
    /// <returns>
    /// A <see cref="Task"/> that completes when the application exits normally or through
    /// cooperative cancellation, and faults after cleanup for an unexpected exception.
    /// </returns>
    /// <example>
    /// <code>
    /// await new NeedlrBootstrapper().RunAsync(async (ctx, ct) =>
    /// {
    ///     ctx.Logger.LogInformation("Application starting...");
    ///     var path = ctx.BootstrapConfiguration["SomeSetting"];
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

        var configBuilder = new ConfigurationBuilder();
        ConfigureBootstrapConfigurationBuilder?.Invoke(configBuilder);
        var bootstrapConfiguration = configBuilder.Build();

        try
        {
            await runAsync(
                new NeedlrBootstrapContext
                {
                    Logger = logger,
                    BootstrapConfiguration = bootstrapConfiguration,
                },
                cancellationToken)
                .ConfigureAwait(false);
        }
        // Cooperative shutdown requested through this token is an intended exit, not a
        // failure, so it must not log critically or fault the returned task. Any other
        // cancellation still propagates through the general handler below.
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            NeedlrBootstrapperLog.ApplicationTerminatedUnexpectedly(logger, ex);
            throw;
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

            (bootstrapConfiguration as IDisposable)?.Dispose();
        }
    }
}
