namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Creates one reusable Langfuse lifecycle for a statistical trial.
/// </summary>
internal interface ILangfuseExperimentTrialLifecycleFactory
{
    ValueTask<LangfuseExperimentTrialLifecycle> EnterAsync(
        LangfuseExperimentTrialLifecycleRequest request,
        CancellationToken cancellationToken);
}
