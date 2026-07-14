namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Configures how unknown binary samples affect a statistical policy.
/// </summary>
public enum ExperimentUnknownSampleTreatment
{
    /// <summary>Exclude unknown samples and force an inconclusive decision.</summary>
    Inconclusive,

    /// <summary>Count unknown samples as denominator failures.</summary>
    CountAsFailure,
}
