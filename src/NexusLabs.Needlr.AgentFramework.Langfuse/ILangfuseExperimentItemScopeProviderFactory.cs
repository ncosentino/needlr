namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Creates built-in hosted and local Langfuse item-scope providers.
/// </summary>
internal interface ILangfuseExperimentItemScopeProviderFactory
{
    LangfuseExperimentItemScopeProvider<TCase, TOutput>
        CreateExperimentItemScopeProvider<TCase, TOutput>(
            ILangfuseExperimentRun run,
            LangfuseExperimentItemScopeOptions<TCase>? options);

    LangfuseExperimentItemScopeProvider<TCase, TOutput>
        CreateLocalExperimentItemScopeProvider<TCase, TOutput>(
            LangfuseExperimentItemScopeOptions<TCase>? options);
}
