namespace NexusLabs.Needlr.AgentFramework.Diagnostics;

/// <summary>
/// Records agent execution metrics for observability. The default implementation emits
/// <see cref="System.Diagnostics.Metrics.Meter"/> counters/histograms and
/// <see cref="System.Diagnostics.ActivitySource"/> activities compatible with OpenTelemetry.
/// </summary>
/// <remarks>
/// <para>
/// Registered via DI — consumers can replace with a no-op or custom implementation.
/// The diagnostics middleware automatically calls these methods; tools and orchestrators
/// do not need to call them directly.
/// </para>
/// <para>
/// If OpenTelemetry is wired in the host (e.g., via <c>AddOpenTelemetry()</c>), these
/// metrics are exported automatically. No needlr-specific configuration required.
/// </para>
/// </remarks>
public interface IAgentMetrics
{
    /// <summary>Records that an agent run has started.</summary>
    /// <param name="agentName">The name of the agent.</param>
    void RecordRunStarted(string agentName);

    /// <summary>Records that an agent run has completed with the given diagnostics.</summary>
    /// <param name="diagnostics">The completed run's diagnostics.</param>
    void RecordRunCompleted(IAgentRunDiagnostics diagnostics);

    /// <summary>Records a completed tool call.</summary>
    /// <param name="toolName">The tool that was invoked.</param>
    /// <param name="duration">How long the tool call took.</param>
    /// <param name="succeeded">Whether the tool call succeeded.</param>
    /// <param name="agentName">The name of the agent that invoked the tool, or <see langword="null"/> if unknown.</param>
    void RecordToolCall(string toolName, TimeSpan duration, bool succeeded, string? agentName = null);

    /// <summary>Records a completed LLM chat completion call.</summary>
    /// <param name="model">The model identifier.</param>
    /// <param name="duration">How long the completion took.</param>
    /// <param name="succeeded">Whether the completion succeeded.</param>
    /// <param name="agentName">The name of the agent that triggered the completion, or <see langword="null"/> if unknown.</param>
    void RecordChatCompletion(string model, TimeSpan duration, bool succeeded, string? agentName = null);

    /// <summary>
    /// Gets the <see cref="System.Diagnostics.ActivitySource"/> for creating distributed
    /// tracing spans. Middleware uses this to create <see cref="System.Diagnostics.Activity"/>
    /// instances for agent runs, tool calls, and chat completions that are exported via
    /// OpenTelemetry when a listener is registered.
    /// </summary>
    System.Diagnostics.ActivitySource ActivitySource { get; }
}
