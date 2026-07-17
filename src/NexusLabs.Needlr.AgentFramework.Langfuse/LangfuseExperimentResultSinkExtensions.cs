namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Creates provider-neutral result sinks from the built-in Langfuse client or session.
/// </summary>
public static class LangfuseExperimentResultSinkExtensions
{
    /// <summary>
    /// Creates a sink that projects canonical item metrics, successful run evaluations, and an
    /// optional decision score through one hosted Langfuse experiment run.
    /// </summary>
    /// <typeparam name="TCase">The caller-owned case value type.</typeparam>
    /// <typeparam name="TOutput">The caller-owned output type.</typeparam>
    /// <param name="client">The built-in Langfuse client or session.</param>
    /// <param name="run">The hosted run that owns dataset-run identity and run-score state.</param>
    /// <param name="options">Optional projection identity and publication behavior.</param>
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
            ILangfuseExperimentRun run,
            LangfuseExperimentResultSinkOptions<TCase, TOutput>? options = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(run);
        return GetFactory(client).CreateExperimentResultSink(
            run,
            options);
    }

    /// <summary>
    /// Creates a local sink that projects canonical item metrics to trace scores without creating
    /// dataset-run scores.
    /// </summary>
    /// <typeparam name="TCase">The caller-owned case value type.</typeparam>
    /// <typeparam name="TOutput">The caller-owned output type.</typeparam>
    /// <param name="client">The built-in Langfuse client or session.</param>
    /// <param name="options">Optional projection identity and publication behavior.</param>
    /// <returns>The local Langfuse result sink.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="client"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">
    /// <paramref name="client"/> does not expose the built-in Langfuse result-sink capability.
    /// </exception>
    public static LangfuseExperimentResultSink<TCase, TOutput>
        CreateLocalExperimentResultSink<TCase, TOutput>(
            this ILangfuseClient client,
            LangfuseExperimentResultSinkOptions<TCase, TOutput>? options = null)
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
