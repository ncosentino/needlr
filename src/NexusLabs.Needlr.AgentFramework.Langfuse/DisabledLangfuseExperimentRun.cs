namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Inert <see cref="ILangfuseExperimentRun"/> returned by a disabled session. Executes callbacks
/// with inert scenarios and links nothing, so experiment code runs unchanged without credentials.
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
    public async Task<LangfuseExperimentItemResult<T>> RunItemAsync<T>(
        string datasetItemId,
        Func<ILangfuseScenario, CancellationToken, Task<T>> callback,
        LangfuseExperimentItemOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(datasetItemId);
        ArgumentNullException.ThrowIfNull(callback);
        cancellationToken.ThrowIfCancellationRequested();

        options ??= new LangfuseExperimentItemOptions();
        options.Validate();

        using var scenario = new DisabledLangfuseScenario();
        var value = await callback(scenario, cancellationToken).ConfigureAwait(false);
        return new LangfuseExperimentItemResult<T>(
            value,
            traceId: null,
            LangfuseExperimentItemLinkStatus.Disabled);
    }
}
