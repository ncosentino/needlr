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
}
