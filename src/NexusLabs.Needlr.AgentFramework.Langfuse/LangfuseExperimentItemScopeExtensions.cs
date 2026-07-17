namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Creates provider-neutral experiment item scopes from the built-in Langfuse client or session.
/// </summary>
public static class LangfuseExperimentItemScopeExtensions
{
    /// <summary>
    /// Creates an item-scope provider that links each statistical trial to the supplied hosted
    /// Langfuse experiment run.
    /// </summary>
    /// <typeparam name="TCase">The caller-owned case value type.</typeparam>
    /// <typeparam name="TOutput">The caller-owned output type.</typeparam>
    /// <param name="client">The built-in Langfuse client or session.</param>
    /// <param name="run">The run that owns shared dataset-run identity and publication state.</param>
    /// <param name="options">Optional trace and publication behavior.</param>
    /// <returns>The item-scope provider to add to an experiment definition.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="client"/> or <paramref name="run"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// <paramref name="client"/> does not expose the built-in Langfuse trial lifecycle.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="run"/> does not expose the built-in Langfuse trial lifecycle.
    /// </exception>
    public static LangfuseExperimentItemScopeProvider<TCase, TOutput>
        CreateExperimentItemScopeProvider<TCase, TOutput>(
            this ILangfuseClient client,
            ILangfuseExperimentRun run,
            LangfuseExperimentItemScopeOptions<TCase>? options = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(run);
        return GetFactory(client).CreateExperimentItemScopeProvider<TCase, TOutput>(
            run,
            options);
    }

    /// <summary>
    /// Creates an item-scope provider that records one trace per statistical trial without creating
    /// a Langfuse dataset run.
    /// </summary>
    /// <typeparam name="TCase">The caller-owned case value type.</typeparam>
    /// <typeparam name="TOutput">The caller-owned output type.</typeparam>
    /// <param name="client">The built-in Langfuse client or session.</param>
    /// <param name="options">Optional trace and publication behavior.</param>
    /// <returns>The local trace-only item-scope provider.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="client"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">
    /// <paramref name="client"/> does not expose the built-in Langfuse trial lifecycle.
    /// </exception>
    public static LangfuseExperimentItemScopeProvider<TCase, TOutput>
        CreateLocalExperimentItemScopeProvider<TCase, TOutput>(
            this ILangfuseClient client,
            LangfuseExperimentItemScopeOptions<TCase>? options = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        return GetFactory(client).CreateLocalExperimentItemScopeProvider<TCase, TOutput>(
            options);
    }

    private static ILangfuseExperimentItemScopeProviderFactory GetFactory(
        ILangfuseClient client) =>
        client.ResolveExperimentFactory<ILangfuseExperimentItemScopeProviderFactory>(
            "The supplied Langfuse client does not expose the built-in experiment trial lifecycle.");
}
