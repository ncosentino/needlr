namespace NexusLabs.Needlr.AgentFramework.Evaluation.Tests.Experiments;

/// <summary>
/// Invokes a callback once when disposed.
/// </summary>
internal sealed class CallbackDisposable(Action callback) : IDisposable
{
    private bool _disposed;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        callback();
    }
}
