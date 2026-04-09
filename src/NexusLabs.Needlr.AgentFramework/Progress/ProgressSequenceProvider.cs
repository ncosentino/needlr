namespace NexusLabs.Needlr.AgentFramework.Progress;

/// <summary>
/// Default <see cref="IProgressSequence"/> implementation using <see cref="Interlocked.Increment(ref long)"/>.
/// </summary>
[DoNotAutoRegister]
internal sealed class ProgressSequenceProvider : IProgressSequence
{
    private long _counter;

    /// <inheritdoc />
    public long Next() => Interlocked.Increment(ref _counter);
}
