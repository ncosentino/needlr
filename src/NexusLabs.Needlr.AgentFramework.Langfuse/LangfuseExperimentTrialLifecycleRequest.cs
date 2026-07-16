namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Contains the frozen scenario and optional hosted-item binding for one statistical trial.
/// </summary>
internal sealed record LangfuseExperimentTrialLifecycleRequest
{
    public LangfuseExperimentTrialLifecycleRequest(
        string scenarioName,
        string? datasetItemId,
        IEnumerable<string>? tags,
        IReadOnlyDictionary<string, string>? metadata,
        LangfuseExperimentItemLinkFailureMode linkFailureMode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scenarioName);
        if (datasetItemId is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(datasetItemId);
        }

        if (!Enum.IsDefined(linkFailureMode))
        {
            throw new ArgumentOutOfRangeException(
                nameof(linkFailureMode),
                linkFailureMode,
                "The experiment item link failure mode is not defined.");
        }

        ScenarioName = scenarioName;
        DatasetItemId = datasetItemId;
        Tags = tags?
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .ToArray() ?? [];
        Metadata = metadata?
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Key))
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal)
            ?? new Dictionary<string, string>(StringComparer.Ordinal);
        LinkFailureMode = linkFailureMode;
    }

    public string ScenarioName { get; }

    public string? DatasetItemId { get; }

    public IReadOnlyList<string> Tags { get; }

    public IReadOnlyDictionary<string, string> Metadata { get; }

    public LangfuseExperimentItemLinkFailureMode LinkFailureMode { get; }
}
