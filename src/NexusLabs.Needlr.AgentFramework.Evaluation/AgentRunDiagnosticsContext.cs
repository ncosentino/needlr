using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;

using NexusLabs.Needlr.AgentFramework.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Evaluation;

/// <summary>
/// Carries an <see cref="IAgentRunDiagnostics"/> snapshot through the
/// <c>Microsoft.Extensions.AI.Evaluation</c> evaluator pipeline so that Needlr-native
/// deterministic evaluators can score execution-mode, tool-call trajectory, and
/// termination behaviour without being re-invoked against the LLM.
/// </summary>
/// <remarks>
/// <para>
/// Evaluators that require the raw diagnostics snapshot (for example,
/// <see cref="ToolCallTrajectoryEvaluator"/>) look up the single instance of this
/// context in the <c>additionalContext</c> collection passed to
/// <see cref="IEvaluator.EvaluateAsync"/>.
/// </para>
/// <para>
/// <see cref="EvaluationContext.Contents"/> contains a single <see cref="TextContent"/>
/// summarising the diagnostics so that reporting pipelines which only serialise
/// <see cref="EvaluationContext.Contents"/> still record meaningful information.
/// Consumers that need the full snapshot read <see cref="Diagnostics"/> directly.
/// </para>
/// </remarks>
public sealed class AgentRunDiagnosticsContext : EvaluationContext
{
    /// <summary>
    /// The stable name used for this context. Evaluators can locate the context by
    /// matching <see cref="EvaluationContext.Name"/> against this value.
    /// </summary>
    public const string ContextName = "Needlr Agent Run Diagnostics";

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentRunDiagnosticsContext"/> class.
    /// </summary>
    /// <param name="diagnostics">The captured agent-run diagnostics to expose to evaluators.</param>
    /// <exception cref="ArgumentNullException"><paramref name="diagnostics"/> is <see langword="null"/>.</exception>
    public AgentRunDiagnosticsContext(IAgentRunDiagnostics diagnostics)
        : base(ContextName, BuildContents(diagnostics))
    {
        Diagnostics = diagnostics;
    }

    /// <summary>Gets the captured diagnostics snapshot.</summary>
    public IAgentRunDiagnostics Diagnostics { get; }

    private static AIContent[] BuildContents(IAgentRunDiagnostics diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        var summary =
            $"Agent='{diagnostics.AgentName}' " +
            $"ExecutionMode='{diagnostics.ExecutionMode ?? "(unknown)"}' " +
            $"Succeeded={diagnostics.Succeeded} " +
            $"ChatCompletions={diagnostics.ChatCompletions.Count} " +
            $"ToolCalls={diagnostics.ToolCalls.Count} " +
            $"DurationMs={(long)diagnostics.TotalDuration.TotalMilliseconds}";

        return [new TextContent(summary)];
    }
}
