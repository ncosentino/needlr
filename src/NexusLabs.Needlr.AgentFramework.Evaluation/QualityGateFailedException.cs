namespace NexusLabs.Needlr.AgentFramework.Evaluation;

/// <summary>
/// Thrown by <see cref="EvaluationQualityGate.Assert"/> when one or more
/// evaluation metrics violate their configured thresholds.
/// </summary>
public sealed class QualityGateFailedException : Exception
{
    /// <summary>Gets the individual threshold violations that triggered the failure.</summary>
    public IReadOnlyList<string> Violations { get; }

    /// <summary>
    /// Creates a new exception from the list of threshold violations.
    /// </summary>
    /// <param name="violations">One or more violation descriptions.</param>
    public QualityGateFailedException(IReadOnlyList<string> violations)
        : base($"Quality gate failed with {violations.Count} violation(s):\n" +
               string.Join("\n", violations.Select(v => $"  • {v}")))
    {
        Violations = violations;
    }
}
