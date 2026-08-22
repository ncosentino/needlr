using Microsoft.Extensions.Logging;

namespace NexusLabs.Needlr.Hosting;

/// <summary>
/// Provides generated logging for <see cref="NeedlrBootstrapper"/>.
/// </summary>
internal static partial class NeedlrBootstrapperLog
{
    [LoggerMessage(
        Level = LogLevel.Critical,
        Message = "Application terminated unexpectedly.")]
    internal static partial void ApplicationTerminatedUnexpectedly(
        ILogger logger,
        Exception exception);
}
