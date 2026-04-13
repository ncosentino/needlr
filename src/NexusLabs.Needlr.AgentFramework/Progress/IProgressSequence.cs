namespace NexusLabs.Needlr.AgentFramework.Progress;

/// <summary>
/// Provides globally-ordered sequence numbers for progress events across all
/// concurrent orchestrations. Registered as a singleton in DI so all
/// <see cref="IProgressReporter"/> instances share a single monotonic counter.
/// </summary>
/// <remarks>
/// <para>
/// Sequence numbers establish a total order across events from different agents,
/// workflows, and threads. This is useful for reconstructing the timeline of a
/// multi-agent orchestration from interleaved event streams.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var seq = serviceProvider.GetRequiredService&lt;IProgressSequence&gt;();
/// var first = seq.Next();  // e.g., 1
/// var second = seq.Next(); // e.g., 2 — guaranteed > first
/// </code>
/// </example>
public interface IProgressSequence
{
    /// <summary>
    /// Allocates the next globally-ordered sequence number. Thread-safe and
    /// guaranteed to return a value greater than all previously returned values.
    /// </summary>
    long Next();
}
