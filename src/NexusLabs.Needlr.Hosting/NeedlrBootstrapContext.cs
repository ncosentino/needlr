using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace NexusLabs.Needlr.Hosting;

/// <summary>
/// Contextual data passed to the <see cref="NeedlrBootstrapper.RunAsync"/> callback.
/// Provides access to a bootstrap logger and a bootstrap <see cref="IConfiguration"/>
/// that are available before the DI container is built.
/// </summary>
/// <remarks>
/// <para>
/// Everything on this context is <strong>bootstrap-phase only</strong>. These resources
/// exist solely to bridge the gap until the DI container is built. Once Syringe (or any
/// host builder) creates the real <see cref="IConfiguration"/> and logging pipeline,
/// the bootstrap versions are disposed and should no longer be referenced.
/// </para>
/// <para>
/// In particular, <see cref="BootstrapConfiguration"/> is <strong>not</strong> the same
/// <see cref="IConfiguration"/> that the application's DI container will provide.
/// They are independent instances that may read different sources, have different
/// precedence rules, or contain different values. Do not cache or leak the bootstrap
/// configuration beyond the callback.
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
///         var logPath = ctx.BootstrapConfiguration["Logging:Path"]
///             ?? "logs/bootstrap.log";
///         ctx.Logger.LogInformation("Bootstrap log path: {Path}", logPath);
///
///         // Build the real app — Syringe creates its own IConfiguration independently.
///         var host = new Syringe()
///             .UsingSourceGen()
///             .ForHost()
///             .UsingOptions(() => CreateHostOptions.Default.UsingLogger(ctx.Logger))
///             .BuildHost();
///         await host.RunAsync(ct);
///     });
/// </code>
/// </example>
public sealed record NeedlrBootstrapContext
{
    /// <summary>
    /// Gets the bootstrap logger, available before the DI container is configured.
    /// </summary>
    /// <remarks>
    /// This logger is for bootstrap-phase diagnostics only. Once the DI container is built,
    /// the application's own <c>ILogger&lt;T&gt;</c> pipeline takes over.
    /// </remarks>
    public required ILogger Logger { get; init; }

    /// <summary>
    /// Gets a minimal <see cref="IConfiguration"/> built during the bootstrap phase,
    /// before the DI container exists.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This configuration is for <strong>bootstrap-phase use only</strong>. It is
    /// <strong>not</strong> the same <see cref="IConfiguration"/> that will be available
    /// after the DI container is built. Syringe (and the .NET Generic Host / WebApplication
    /// builder) builds its own <see cref="IConfiguration"/> independently. The two may read
    /// the same files but are separate instances with no shared state.
    /// </para>
    /// <para>
    /// By default the bootstrap configuration is <strong>empty</strong>. Use
    /// <see cref="NeedlrBootstrapperExtensions.ConfigureBootstrapConfiguration"/> to add
    /// configuration sources (JSON files, environment variables, in-memory collections, etc.)
    /// that are needed during the bootstrap phase.
    /// </para>
    /// </remarks>
    public required IConfiguration BootstrapConfiguration { get; init; }
}
