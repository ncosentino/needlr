using NexusLabs.Needlr.AgentFramework.Progress;

namespace NexusLabs.Needlr.AgentFramework.Diagnostics;

/// <summary>
/// Options for running a workflow with diagnostics and progress reporting.
/// Consolidates the parameters for <c>RunWithDiagnosticsAsync</c> into a
/// single record to avoid excessive parameter counts.
/// </summary>
[DoNotAutoRegister]
public sealed record WorkflowRunOptions
{
    /// <summary>Gets the diagnostics accessor for per-agent captures.</summary>
    public required IAgentDiagnosticsAccessor DiagnosticsAccessor { get; init; }

    /// <summary>Gets the optional progress reporter for real-time events.</summary>
    public IProgressReporter? ProgressReporter { get; init; }

    /// <summary>Gets the optional collector for per-LLM-call completions.</summary>
    public IChatCompletionCollector? CompletionCollector { get; init; }

    /// <summary>
    /// Gets the optional accessor for threading the progress reporter through AsyncLocal
    /// so middleware can emit LLM/tool call events.
    /// </summary>
    public IProgressReporterAccessor? ProgressReporterAccessor { get; init; }

    /// <summary>Gets the cancellation token.</summary>
    public CancellationToken CancellationToken { get; init; }
}
