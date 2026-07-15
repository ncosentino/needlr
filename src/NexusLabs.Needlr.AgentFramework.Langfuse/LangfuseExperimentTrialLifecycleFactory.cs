namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Creates inactive scenarios and performs at most one hosted item link before returning a trial
/// lifecycle.
/// </summary>
[DoNotAutoRegister]
internal sealed class LangfuseExperimentTrialLifecycleFactory(
    Func<LangfuseExperimentTrialLifecycleRequest, ILangfuseActivatableScenario> createScenario,
    ILangfuseExperimentItemLinker? itemLinker) :
    ILangfuseExperimentTrialLifecycleFactory
{
    public async ValueTask<LangfuseExperimentTrialLifecycle> EnterAsync(
        LangfuseExperimentTrialLifecycleRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var scenario = createScenario(request);
        ArgumentNullException.ThrowIfNull(scenario);
        try
        {
            var recordedTraceId = scenario.Activity?.Recorded == true
                ? scenario.TraceId
                : null;
            LangfuseExperimentItemLinkResult? link = null;
            if (request.DatasetItemId is { } datasetItemId)
            {
                if (itemLinker is null)
                {
                    throw new InvalidOperationException(
                        "A hosted Langfuse trial requires an experiment item linker.");
                }

                link = await itemLinker
                    .CreateLinkAsync(
                        datasetItemId,
                        recordedTraceId,
                        request.LinkFailureMode,
                        cancellationToken)
                    .ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
            }

            return new LangfuseExperimentTrialLifecycle(
                scenario,
                recordedTraceId,
                link);
        }
        catch
        {
            scenario.Dispose();
            throw;
        }
    }
}
