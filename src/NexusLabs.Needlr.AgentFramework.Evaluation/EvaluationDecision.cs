namespace NexusLabs.Needlr.AgentFramework.Evaluation;

/// <summary>
/// Identifies the outcome of a structured quality evaluation.
/// </summary>
public enum EvaluationDecision
{
    /// <summary>The available evidence satisfies the configured requirements.</summary>
    Passed,

    /// <summary>The available evidence violates one or more configured requirements.</summary>
    Failed,

    /// <summary>The available evidence cannot support a pass or failure decision.</summary>
    Inconclusive,
}
