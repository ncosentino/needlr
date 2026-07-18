namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Builds the hosted item-scope provider shared by the enabled and disabled
/// <see cref="ILangfuseClient"/> implementations of
/// <see cref="ILangfuseExperimentItemScopeProviderFactory.CreateExperimentItemScopeProvider{TCase, TOutput}"/>.
/// </summary>
internal static class LangfuseHostedExperimentItemScopeProviderExtensions
{
    public static LangfuseExperimentItemScopeProvider<TCase, TOutput>
        CreateHostedExperimentItemScopeProvider<TCase, TOutput>(
        this ILangfuseExperimentRun run,
        LangfuseExperimentItemScopeOptions<TCase>? options)
    {
        ArgumentNullException.ThrowIfNull(run);
        if (run is not ILangfuseExperimentTrialLifecycleFactory lifecycleFactory)
        {
            throw new ArgumentException(
                "The supplied experiment run does not expose the built-in Langfuse trial lifecycle.",
                nameof(run));
        }

        return new LangfuseExperimentItemScopeProvider<TCase, TOutput>(
            lifecycleFactory,
            linkHostedItem: true,
            options);
    }
}
