namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Configures the scenario and dataset-link behavior for one experiment item execution.
/// </summary>
public sealed class LangfuseExperimentItemOptions
{
    /// <summary>
    /// Gets or sets the trace name. When omitted or whitespace, a name is derived from the dataset
    /// and item identifiers.
    /// </summary>
    public string? ScenarioName { get; init; }

    /// <summary>
    /// Gets or sets optional tags applied to the item trace.
    /// </summary>
    public IEnumerable<string>? Tags { get; init; }

    /// <summary>
    /// Gets or sets optional filterable metadata applied to the item trace.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }

    /// <summary>
    /// Gets or sets the behavior when Langfuse cannot link the item trace to the dataset run.
    /// </summary>
    public LangfuseExperimentItemLinkFailureMode LinkFailureMode { get; init; } =
        LangfuseExperimentItemLinkFailureMode.BestEffort;

    internal void Validate()
    {
        if (!Enum.IsDefined(LinkFailureMode))
        {
            throw new ArgumentOutOfRangeException(
                nameof(LinkFailureMode),
                LinkFailureMode,
                "The experiment item link failure mode is not defined.");
        }
    }
}
