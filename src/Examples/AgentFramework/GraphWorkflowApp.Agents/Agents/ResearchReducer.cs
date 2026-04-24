using NexusLabs.Needlr.AgentFramework;

namespace GraphWorkflowApp.Agents;

/// <summary>
/// Deterministic reducer for the research-pipeline graph. Merges branch outputs
/// from <see cref="WebResearchAgent"/> and <see cref="DatabaseAgent"/> into a
/// single string without incurring LLM cost. The <see cref="SummarizerAgent"/>
/// then synthesizes this merged text into a final report.
/// </summary>
[AgentGraphReducer("research-pipeline", ReducerMethod = nameof(MergeResults))]
public static class ResearchReducer
{
    /// <summary>
    /// Concatenates all branch outputs, separated by a visual delimiter, so the
    /// downstream <see cref="SummarizerAgent"/> receives a unified input.
    /// </summary>
    public static string MergeResults(IReadOnlyList<string> branchOutputs)
        => string.Join("\n\n--- Branch Result ---\n\n", branchOutputs);
}
