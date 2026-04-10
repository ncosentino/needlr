namespace NexusLabs.Needlr.AgentFramework.Progress;

/// <summary>
/// Default <see cref="IProgressReporterErrorHandler"/> that discards exceptions.
/// Registered by <c>UsingAgentFramework()</c> unless the consumer registers a replacement.
/// </summary>
[DoNotAutoRegister]
internal sealed class NullProgressReporterErrorHandler : IProgressReporterErrorHandler
{
    public void OnSinkException(IProgressSink sink, IProgressEvent progressEvent, Exception exception)
    {
    }
}
