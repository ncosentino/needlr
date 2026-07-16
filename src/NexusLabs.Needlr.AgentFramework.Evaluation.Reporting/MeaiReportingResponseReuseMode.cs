namespace NexusLabs.Needlr.AgentFramework.Evaluation.Reporting;

/// <summary>
/// Controls how MEAI Reporting response-cache entries are reused by experiment trials.
/// </summary>
public enum MeaiReportingResponseReuseMode
{
    /// <summary>
    /// Reuses matching responses across experiment runs by case and trial identity.
    /// </summary>
    CaseAndTrialReplay,

    /// <summary>
    /// Reuses matching responses across retries within one run but isolates separate run IDs.
    /// </summary>
    FreshPerRun,

    /// <summary>
    /// Disables MEAI Reporting response caching.
    /// </summary>
    Disabled,
}
