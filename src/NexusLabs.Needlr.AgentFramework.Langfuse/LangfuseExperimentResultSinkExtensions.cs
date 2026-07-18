namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Creates provider-neutral result sinks from the built-in Langfuse client or session.
/// </summary>
public static class LangfuseExperimentResultSinkExtensions
{
    /// <summary>
    /// Creates a sink that projects canonical item metrics, successful run evaluations, and an
    /// optional decision score through one hosted Langfuse experiment run using default projection
    /// identity and publication behavior.
    /// </summary>
    /// <typeparam name="TCase">The caller-owned case value type.</typeparam>
    /// <typeparam name="TOutput">The caller-owned output type.</typeparam>
    /// <param name="client">The built-in Langfuse client or session.</param>
    /// <param name="run">The hosted run that owns dataset-run identity and run-score state.</param>
    /// <returns>The Langfuse result sink.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="client"/> or <paramref name="run"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// <paramref name="client"/> does not expose the built-in Langfuse result-sink capability.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="run"/> does not expose structured built-in score publication.
    /// </exception>
    public static LangfuseExperimentResultSink<TCase, TOutput>
        CreateExperimentResultSink<TCase, TOutput>(
            this ILangfuseClient client,
            ILangfuseExperimentRun run) =>
        CreateExperimentResultSinkCore<TCase, TOutput>(
            client,
            run,
            options: null);

    /// <summary>
    /// Creates a sink that projects canonical item metrics, successful run evaluations, and an
    /// optional decision score through one hosted Langfuse experiment run using explicit projection
    /// identity and publication behavior.
    /// </summary>
    /// <typeparam name="TCase">The caller-owned case value type.</typeparam>
    /// <typeparam name="TOutput">The caller-owned output type.</typeparam>
    /// <param name="client">The built-in Langfuse client or session.</param>
    /// <param name="run">The hosted run that owns dataset-run identity and run-score state.</param>
    /// <param name="options">The projection identity and publication behavior.</param>
    /// <returns>The Langfuse result sink.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="client"/>, <paramref name="run"/>, or <paramref name="options"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// <paramref name="client"/> does not expose the built-in Langfuse result-sink capability.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="run"/> does not expose structured built-in score publication.
    /// </exception>
    public static LangfuseExperimentResultSink<TCase, TOutput>
        CreateExperimentResultSink<TCase, TOutput>(
            this ILangfuseClient client,
            ILangfuseExperimentRun run,
            LangfuseExperimentResultSinkOptions<TCase, TOutput> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return CreateExperimentResultSinkCore<TCase, TOutput>(
            client,
            run,
            options);
    }

    /// <summary>
    /// Creates a local sink that projects canonical item metrics to trace scores without creating
    /// dataset-run scores, using default projection identity and publication behavior.
    /// </summary>
    /// <typeparam name="TCase">The caller-owned case value type.</typeparam>
    /// <typeparam name="TOutput">The caller-owned output type.</typeparam>
    /// <param name="client">The built-in Langfuse client or session.</param>
    /// <returns>The local Langfuse result sink.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="client"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">
    /// <paramref name="client"/> does not expose the built-in Langfuse result-sink capability.
    /// </exception>
    public static LangfuseExperimentResultSink<TCase, TOutput>
        CreateLocalExperimentResultSink<TCase, TOutput>(
            this ILangfuseClient client) =>
        CreateLocalExperimentResultSinkCore<TCase, TOutput>(
            client,
            options: null);

    /// <summary>
    /// Creates a local sink that projects canonical item metrics to trace scores without creating
    /// dataset-run scores, using explicit projection identity and publication behavior.
    /// </summary>
    /// <typeparam name="TCase">The caller-owned case value type.</typeparam>
    /// <typeparam name="TOutput">The caller-owned output type.</typeparam>
    /// <param name="client">The built-in Langfuse client or session.</param>
    /// <param name="options">The projection identity and publication behavior.</param>
    /// <returns>The local Langfuse result sink.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="client"/> or <paramref name="options"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// <paramref name="client"/> does not expose the built-in Langfuse result-sink capability.
    /// </exception>
    public static LangfuseExperimentResultSink<TCase, TOutput>
        CreateLocalExperimentResultSink<TCase, TOutput>(
            this ILangfuseClient client,
            LangfuseExperimentResultSinkOptions<TCase, TOutput> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return CreateLocalExperimentResultSinkCore<TCase, TOutput>(
            client,
            options);
    }

    private static LangfuseExperimentResultSink<TCase, TOutput>
        CreateExperimentResultSinkCore<TCase, TOutput>(
            ILangfuseClient client,
            ILangfuseExperimentRun run,
            LangfuseExperimentResultSinkOptions<TCase, TOutput>? options)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(run);
        return GetFactory(client).CreateExperimentResultSink(
            run,
            options);
    }

    private static LangfuseExperimentResultSink<TCase, TOutput>
        CreateLocalExperimentResultSinkCore<TCase, TOutput>(
            ILangfuseClient client,
            LangfuseExperimentResultSinkOptions<TCase, TOutput>? options)
    {
        ArgumentNullException.ThrowIfNull(client);
        return GetFactory(client).CreateLocalExperimentResultSink(
            options);
    }

    private static ILangfuseExperimentResultSinkFactory GetFactory(
        ILangfuseClient client) =>
        client.ResolveExperimentFactory<ILangfuseExperimentResultSinkFactory>(
            "The supplied Langfuse client does not expose the built-in experiment result-sink capability.");
}
