using Microsoft.Extensions.Logging;

namespace NexusLabs.Needlr.Hosting;

/// <summary>
/// Contextual data passed to the <see cref="NeedlrBootstrapper.RunAsync"/> callback.
/// Provides access to a bootstrap logger that is available before the DI container is built.
/// </summary>
/// <example>
/// <code>
/// await new NeedlrBootstrapper().RunAsync(async (ctx, ct) =>
/// {
///     ctx.Logger.LogInformation("Application starting...");
///     // build your host, run your app, etc.
/// });
/// </code>
/// </example>
public sealed record NeedlrBootstrapContext
{
    /// <summary>
    /// Gets the bootstrap logger, available before the DI container is configured.
    /// </summary>
    public required ILogger Logger { get; init; }
}
