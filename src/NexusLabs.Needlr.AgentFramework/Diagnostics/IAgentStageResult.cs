namespace NexusLabs.Needlr.AgentFramework.Diagnostics;

/// <summary>
/// Result of a single agent stage within a multi-agent pipeline or group chat workflow,
/// combining the response text with the captured diagnostics for that stage.
/// </summary>
/// <remarks>
/// <para>
/// Access stage results via <see cref="IPipelineRunResult.Stages"/> after calling
/// <c>RunWithDiagnosticsAsync</c>. Each stage corresponds to one agent turn in the
/// workflow.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var result = await workflow.RunWithDiagnosticsAsync(prompt, accessor);
/// foreach (var stage in result.Stages)
/// {
///     var tokens = stage.Diagnostics?.AggregateTokenUsage;
///     Console.WriteLine($"[{stage.AgentName}] {tokens?.TotalTokens ?? 0} tokens, " +
///         $"{stage.Diagnostics?.TotalDuration.TotalSeconds:F1}s");
///     Console.WriteLine($"  Response: {stage.ResponseText[..Math.Min(100, stage.ResponseText.Length)]}...");
/// }
/// </code>
/// </example>
public interface IAgentStageResult
{
    /// <summary>Gets the agent's executor ID (agent name, possibly with a MAF-assigned GUID suffix).</summary>
    string AgentName { get; }

    /// <summary>Gets the text content the agent produced during this stage. Empty if the agent responded only via tool calls.</summary>
    string ResponseText { get; }

    /// <summary>
    /// Gets the diagnostics captured during this agent's execution, including per-call
    /// token usage, tool call details, and timing. <see langword="null"/> if diagnostics
    /// were not enabled via <c>UsingDiagnostics()</c>.
    /// </summary>
    IAgentRunDiagnostics? Diagnostics { get; }
}
