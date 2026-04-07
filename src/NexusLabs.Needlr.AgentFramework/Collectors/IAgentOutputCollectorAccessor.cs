namespace NexusLabs.Needlr.AgentFramework.Collectors;

/// <summary>
/// <see cref="AsyncLocal{T}"/>-scoped accessor for <see cref="IAgentOutputCollector{T}"/>.
/// Each pipeline run gets its own collector scope, isolated from concurrent runs.
/// </summary>
/// <typeparam name="T">The record type to collect.</typeparam>
/// <remarks>
/// <para>
/// The orchestrator opens a scope before running an agent stage:
/// </para>
/// <code>
/// var accessor = serviceProvider.GetRequiredService&lt;IAgentOutputCollectorAccessor&lt;ReviewIssue&gt;&gt;();
/// using (accessor.BeginScope())
/// {
///     await agent.RunAsync(prompt);
///     var issues = accessor.Current?.Items ?? [];
/// }
/// </code>
/// <para>
/// Tools inject the accessor and call <see cref="IAgentOutputCollector{T}.Add"/> during execution.
/// </para>
/// </remarks>
public interface IAgentOutputCollectorAccessor<T>
{
    /// <summary>Gets the collector for the current scope, or <see langword="null"/> if no scope is active.</summary>
    IAgentOutputCollector<T>? Current { get; }

    /// <summary>
    /// Opens a new collector scope. Disposing the handle restores the previous scope.
    /// </summary>
    IDisposable BeginScope();

    /// <summary>
    /// Opens a new collector scope using the provided collector instance.
    /// Useful when the orchestrator needs to supply a pre-populated or custom collector.
    /// </summary>
    IDisposable BeginScope(IAgentOutputCollector<T> collector);
}
