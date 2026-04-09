namespace NexusLabs.Needlr.AgentFramework.Progress;

/// <summary>
/// Provides globally-ordered sequence numbers for progress events.
/// Registered as a singleton in DI — resettable in tests.
/// </summary>
public interface IProgressSequence
{
    /// <summary>Allocates the next globally-ordered sequence number.</summary>
    long Next();
}
