namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Provides a caller-owned concurrency boundary that can be shared across experiment runs.
/// </summary>
/// <remarks>
/// The runner acquires one lease per execution attempt and disposes the lease before any retry
/// delay. The caller retains ownership of the limiter and its lifetime.
/// </remarks>
public interface IExperimentConcurrencyLimiter
{
    /// <summary>
    /// Acquires one concurrency lease.
    /// </summary>
    /// <param name="cancellationToken">The caller cancellation token.</param>
    /// <returns>A lease that releases the acquired permit when disposed.</returns>
    ValueTask<IAsyncDisposable> AcquireAsync(CancellationToken cancellationToken);
}
