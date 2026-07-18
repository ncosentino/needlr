namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Creates provider-neutral experiment item scopes from the built-in Langfuse client or session.
/// </summary>
public static class LangfuseExperimentItemScopeExtensions
{
    /// <summary>
    /// Creates an item-scope provider that links each statistical trial to the supplied hosted
    /// Langfuse experiment run using default trace and publication behavior.
    /// </summary>
    /// <typeparam name="TCase">The caller-owned case value type.</typeparam>
    /// <typeparam name="TOutput">The caller-owned output type.</typeparam>
    /// <param name="client">The built-in Langfuse client or session.</param>
    /// <param name="run">The run that owns shared dataset-run identity and publication state.</param>
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
            ILangfuseExperimentRun run) =>
        CreateExperimentItemScopeProviderCore<TCase, TOutput>(
            client,
            run,
            options: null);

    /// <summary>
    /// Creates an item-scope provider that links each statistical trial to the supplied hosted
    /// Langfuse experiment run using explicit trace and publication behavior.
    /// </summary>
    /// <typeparam name="TCase">The caller-owned case value type.</typeparam>
    /// <typeparam name="TOutput">The caller-owned output type.</typeparam>
    /// <param name="client">The built-in Langfuse client or session.</param>
    /// <param name="run">The run that owns shared dataset-run identity and publication state.</param>
    /// <param name="options">The trace and publication behavior.</param>
    /// <returns>The item-scope provider to add to an experiment definition.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="client"/>, <paramref name="run"/>, or <paramref name="options"/> is
    /// <see langword="null"/>.
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
            LangfuseExperimentItemScopeOptions<TCase> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return CreateExperimentItemScopeProviderCore<TCase, TOutput>(
            client,
            run,
            options);
    }

    /// <summary>
    /// Creates an item-scope provider that records one trace per statistical trial without creating
    /// a Langfuse dataset run, using default trace and publication behavior.
    /// </summary>
    /// <typeparam name="TCase">The caller-owned case value type.</typeparam>
    /// <typeparam name="TOutput">The caller-owned output type.</typeparam>
    /// <param name="client">The built-in Langfuse client or session.</param>
    /// <returns>The local trace-only item-scope provider.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="client"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">
    /// <paramref name="client"/> does not expose the built-in Langfuse trial lifecycle.
    /// </exception>
    public static LangfuseExperimentItemScopeProvider<TCase, TOutput>
        CreateLocalExperimentItemScopeProvider<TCase, TOutput>(
            this ILangfuseClient client) =>
        CreateLocalExperimentItemScopeProviderCore<TCase, TOutput>(
            client,
            options: null);

    /// <summary>
    /// Creates an item-scope provider that records one trace per statistical trial without creating
    /// a Langfuse dataset run, using explicit trace and publication behavior.
    /// </summary>
    /// <typeparam name="TCase">The caller-owned case value type.</typeparam>
    /// <typeparam name="TOutput">The caller-owned output type.</typeparam>
    /// <param name="client">The built-in Langfuse client or session.</param>
    /// <param name="options">The trace and publication behavior.</param>
    /// <returns>The local trace-only item-scope provider.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="client"/> or <paramref name="options"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// <paramref name="client"/> does not expose the built-in Langfuse trial lifecycle.
    /// </exception>
    public static LangfuseExperimentItemScopeProvider<TCase, TOutput>
        CreateLocalExperimentItemScopeProvider<TCase, TOutput>(
            this ILangfuseClient client,
            LangfuseExperimentItemScopeOptions<TCase> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return CreateLocalExperimentItemScopeProviderCore<TCase, TOutput>(
            client,
            options);
    }

    private static LangfuseExperimentItemScopeProvider<TCase, TOutput>
        CreateExperimentItemScopeProviderCore<TCase, TOutput>(
            ILangfuseClient client,
            ILangfuseExperimentRun run,
            LangfuseExperimentItemScopeOptions<TCase>? options)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(run);
        return GetFactory(client).CreateExperimentItemScopeProvider<TCase, TOutput>(
            run,
            options);
    }

    private static LangfuseExperimentItemScopeProvider<TCase, TOutput>
        CreateLocalExperimentItemScopeProviderCore<TCase, TOutput>(
            ILangfuseClient client,
            LangfuseExperimentItemScopeOptions<TCase>? options)
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
