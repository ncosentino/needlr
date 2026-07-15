namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Links one scope-owned scenario trace to an experiment run.
/// </summary>
internal interface ILangfuseExperimentItemLinker
{
    ValueTask<LangfuseExperimentItemLinkResult> CreateLinkAsync(
        string datasetItemId,
        string? recordedTraceId,
        LangfuseExperimentItemLinkFailureMode failureMode,
        CancellationToken cancellationToken);
}
