namespace NexusLabs.Needlr.AgentFramework.Evaluation.Reporting;

/// <summary>
/// Defines the stable provider and correlation names emitted by MEAI Reporting experiments.
/// </summary>
public static class MeaiReportingExperimentSchema
{
    /// <summary>Gets the provider name used in item publication results.</summary>
    public const string ProviderName = "meai-reporting";

    /// <summary>Gets the namespace used for MEAI Reporting item correlations.</summary>
    public const string CorrelationNamespace = "meai.reporting";

    /// <summary>Gets the execution-name correlation name.</summary>
    public const string ExecutionNameCorrelationName = "execution.name";

    /// <summary>Gets the scenario-name correlation name.</summary>
    public const string ScenarioNameCorrelationName = "scenario.name";

    /// <summary>Gets the iteration-name correlation name.</summary>
    public const string IterationNameCorrelationName = "iteration.name";
}
