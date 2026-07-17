namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Shared dataset-item lifecycle orchestration used by both the enabled and disabled
/// <see cref="ILangfuseExperimentRun"/> implementations of
/// <see cref="ILangfuseExperimentRun.RunItemAsync{T}(string, Func{ILangfuseScenario, CancellationToken, Task{T}}, LangfuseExperimentItemOptions?, CancellationToken)"/>.
/// </summary>
internal static class LangfuseExperimentTrialLifecycleFactoryExtensions
{
    public static async Task<LangfuseExperimentItemResult<T>> RunItemAsync<T>(
        this LangfuseExperimentTrialLifecycleFactory lifecycleFactory,
        string datasetName,
        string datasetItemId,
        Func<ILangfuseScenario, CancellationToken, Task<T>> callback,
        LangfuseExperimentItemOptions? options,
        string missingLinkMessage,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(datasetItemId);
        ArgumentNullException.ThrowIfNull(callback);
        cancellationToken.ThrowIfCancellationRequested();

        options ??= new LangfuseExperimentItemOptions();
        options.Validate();

        var name = string.IsNullOrWhiteSpace(options.ScenarioName)
            ? $"{datasetName}: {datasetItemId}"
            : options.ScenarioName;

        await using var lifecycle = await lifecycleFactory
            .EnterAsync(
                new LangfuseExperimentTrialLifecycleRequest(
                    name,
                    datasetItemId,
                    options.Tags,
                    options.Metadata,
                    options.LinkFailureMode),
                cancellationToken)
            .ConfigureAwait(false);
        using var activation = lifecycle.Activate();
        var value = await callback(
            lifecycle.Scenario,
            cancellationToken).ConfigureAwait(false);
        return new LangfuseExperimentItemResult<T>(
            value,
            lifecycle.RecordedTraceId,
            lifecycle.Link
                ?? throw new InvalidOperationException(missingLinkMessage));
    }
}
