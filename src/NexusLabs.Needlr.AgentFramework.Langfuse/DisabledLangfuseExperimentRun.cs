namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Inert <see cref="ILangfuseExperimentRun"/> returned by a disabled session. Begins inert
/// scenarios and links nothing, so experiment code runs unchanged without credentials.
/// </summary>
internal sealed class DisabledLangfuseExperimentRun : ILangfuseExperimentRun
{
    public DisabledLangfuseExperimentRun(string datasetName, string runName)
    {
        DatasetName = datasetName;
        RunName = runName;
    }

    /// <inheritdoc />
    public string DatasetName { get; }

    /// <inheritdoc />
    public string RunName { get; }

    /// <inheritdoc />
    public Task<ILangfuseScenario> BeginItemAsync(
        string datasetItemId,
        string? scenarioName = null,
        IEnumerable<string>? tags = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<ILangfuseScenario>(new DisabledLangfuseScenario());
}
