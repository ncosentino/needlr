using NexusLabs.Needlr.Hosting;

using Serilog;
using Serilog.Extensions.Logging;

using Microsoft.Extensions.Configuration;

namespace NexusLabs.Needlr.Serilog;

/// <summary>
/// Wraps an application entry point with Serilog-specific bootstrap lifecycle management:
/// two-stage initialization, a pre-DI Serilog logger, pre-DI <see cref="IConfiguration"/>,
/// top-level exception handling, and automatic log flushing on shutdown.
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
/// <see cref="NeedlrSerilogBootstrapperExtensions.Configure(NeedlrSerilogBootstrapper, Action{LoggerConfiguration})"/>
/// to apply your own <see cref="LoggerConfiguration"/>, or use
/// <see cref="NeedlrSerilogBootstrapperExtensions.Configure(NeedlrSerilogBootstrapper, Action{LoggerConfiguration, IConfiguration})"/>
/// to configure Serilog using the bootstrap <see cref="IConfiguration"/>.
/// </para>
/// <para>
/// By default the bootstrap configuration is <strong>empty</strong>. Use
/// <see cref="NeedlrSerilogBootstrapperExtensions.ConfigureBootstrapConfiguration"/> to add
/// configuration sources needed during the bootstrap phase.
/// The bootstrap configuration is <strong>not</strong> the same <see cref="IConfiguration"/>
/// that the application's DI container will provide.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// await new NeedlrSerilogBootstrapper()
///     .ConfigureBootstrapConfiguration(builder => builder
///         .AddJsonFile("appsettings.json", optional: true))
///     .Configure((cfg, bootstrapConfiguration) => cfg
///         .ReadFrom.Configuration(bootstrapConfiguration)
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
    internal Action<LoggerConfiguration, IConfiguration>? ConfigureWithConfigAction { get; init; }
    internal Action<IConfigurationBuilder>? ConfigureBootstrapConfigurationBuilder { get; init; }

    /// <summary>
    /// Runs the application entry point with Serilog bootstrap lifecycle management.
    /// </summary>
    /// <param name="runAsync">
    /// The application callback. Receives a <see cref="NeedlrBootstrapContext"/> containing
    /// a bootstrap logger backed by the configured Serilog pipeline and a bootstrap
    /// <see cref="IConfiguration"/>.
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

        // Build bootstrap config early so the Serilog Configure delegate can use it.
        // This is built outside NeedlrBootstrapper so we can pass it to Serilog's
        // ReadFrom.Configuration before the inner bootstrapper runs.
        var configBuilder = new ConfigurationBuilder();
        ConfigureBootstrapConfigurationBuilder?.Invoke(configBuilder);
        IConfigurationRoot? bootstrapConfiguration = null;

        try
        {
            bootstrapConfiguration = configBuilder.Build();

            var configuration = new LoggerConfiguration();
            if (ConfigureWithConfigAction is not null)
            {
                ConfigureWithConfigAction(configuration, bootstrapConfiguration);
            }
            else if (ConfigureAction is not null)
            {
                ConfigureAction(configuration);
            }
            else
            {
                configuration.WriteTo.Console();
            }

            Log.Logger = configuration.CreateLogger();
        }
        catch (Exception ex)
        {
            // Serilog configuration failed — fall back to a bare console logger so
            // the error is visible, then rethrow into the inner bootstrapper's
            // catch/cleanup path via a wrapper callback.
            Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();
            Log.Fatal(ex, "Failed to configure Serilog bootstrap logger.");

            // Let the inner bootstrapper handle cleanup. The callback will throw
            // the original exception so it is logged at Critical by NeedlrBootstrapper.
            var capturedEx = ex;
            await RunInnerBootstrapper(
                bootstrapConfiguration,
                (_, _) => throw capturedEx,
                cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        await RunInnerBootstrapper(
            bootstrapConfiguration,
            runAsync,
            cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task RunInnerBootstrapper(
        IConfigurationRoot? bootstrapConfiguration,
        Func<NeedlrBootstrapContext, CancellationToken, Task> runAsync,
        CancellationToken cancellationToken)
    {
        var loggerFactory = new SerilogLoggerFactory(dispose: false);

        var inner = new NeedlrBootstrapper()
            .UsingLoggerFactory(loggerFactory)
            .WithCleanup(() => Log.CloseAndFlushAsync().AsTask());

        if (ConfigureBootstrapConfigurationBuilder is not null)
        {
            inner = inner.ConfigureBootstrapConfiguration(ConfigureBootstrapConfigurationBuilder);
        }

        // If we already built bootstrap config for Serilog, override the inner
        // bootstrapper's config building to reuse the same instance instead of
        // building it twice. We pass a no-op configure action — the inner
        // bootstrapper will still build a ConfigurationBuilder, but we need
        // it to have the same sources. Simpler: we forward the same configure action.
        // The inner bootstrapper will build its own IConfigurationRoot from the
        // same sources — this is acceptable because bootstrap config is cheap
        // and the Serilog config phase is done.

        await inner.RunAsync(runAsync, cancellationToken)
            .ConfigureAwait(false);

        // Dispose our pre-built config after the inner bootstrapper has
        // disposed its own copy and completed cleanup.
        (bootstrapConfiguration as IDisposable)?.Dispose();
    }
}
