namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Provides one completed execution attempt to a retry policy.
/// </summary>
public sealed class ExperimentRetryContext
{
    internal ExperimentRetryContext(
        string runId,
        int sequence,
        string caseId,
        int trialIndex,
        ExperimentAttemptResult attempt)
    {
        RunId = runId;
        Sequence = sequence;
        CaseId = caseId;
        TrialIndex = trialIndex;
        Attempt = attempt;
    }

    /// <summary>Gets the caller-supplied run identifier.</summary>
    public string RunId { get; }

    /// <summary>Gets the zero-based stable item sequence.</summary>
    public int Sequence { get; }

    /// <summary>Gets the case identifier.</summary>
    public string CaseId { get; }

    /// <summary>Gets the one-based statistical trial index.</summary>
    public int TrialIndex { get; }

    /// <summary>Gets the completed execution attempt.</summary>
    public ExperimentAttemptResult Attempt { get; }
}
