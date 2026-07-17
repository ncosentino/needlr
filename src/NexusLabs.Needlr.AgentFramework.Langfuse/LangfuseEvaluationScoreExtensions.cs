using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;

namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Convenience extensions for projecting <c>Microsoft.Extensions.AI.Evaluation</c> results onto a
/// Langfuse scenario trace as scores.
/// </summary>
public static class LangfuseEvaluationScoreExtensions
{
    /// <summary>
    /// Records every metric in <paramref name="result"/> as a Langfuse score on
    /// <paramref name="scenario"/>'s trace. Equivalent to
    /// <see cref="ILangfuseScenario.RecordEvaluationAsync(EvaluationResult)"/>, provided as a fluent
    /// call site for eval code that already holds an <see cref="EvaluationResult"/>.
    /// </summary>
    /// <param name="result">The evaluation result to project.</param>
    /// <param name="scenario">The scenario whose trace the scores attach to.</param>
    /// <returns>A task that completes when Langfuse has accepted all projected scores.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="result"/> or <paramref name="scenario"/> is <see langword="null"/>.
    /// </exception>
    public static Task RecordLangfuseScoresAsync(
        this EvaluationResult result,
        ILangfuseScenario scenario) =>
        RecordLangfuseScoresAsync(
            result,
            scenario,
            options: null,
            CancellationToken.None);

    /// <summary>
    /// Records every metric in <paramref name="result"/> as a Langfuse score on
    /// <paramref name="scenario"/>'s trace. Equivalent to
    /// <see cref="ILangfuseScenario.RecordEvaluationAsync(EvaluationResult, LangfuseEvaluationScoreOptions?, CancellationToken)"/>,
    /// provided as a fluent call site for eval code that already holds an
    /// <see cref="EvaluationResult"/>.
    /// </summary>
    /// <param name="result">The evaluation result to project.</param>
    /// <param name="scenario">The scenario whose trace the scores attach to.</param>
    /// <param name="options">
    /// Stable identity settings for projected metric scores, or <see langword="null"/>.
    /// </param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when Langfuse has accepted all projected scores.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="result"/> or <paramref name="scenario"/> is <see langword="null"/>.
    /// </exception>
    public static Task RecordLangfuseScoresAsync(
        this EvaluationResult result,
        ILangfuseScenario scenario,
        LangfuseEvaluationScoreOptions? options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(scenario);

        return scenario.RecordEvaluationAsync(result, options, cancellationToken);
    }

    /// <summary>
    /// Runs each evaluator over the supplied agent output and records every resulting metric as a
    /// Langfuse score on the scenario's trace using default evaluator and score configuration.
    /// </summary>
    /// <param name="scenario">The scenario whose trace the scores attach to.</param>
    /// <param name="evaluators">The evaluators to run.</param>
    /// <param name="messages">The conversation messages sent to the agent (for example, from <c>EvaluationInputs.Messages</c>).</param>
    /// <param name="modelResponse">The agent's response (for example, from <c>EvaluationInputs.ModelResponse</c>).</param>
    /// <returns>The evaluation results, in evaluator order.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="scenario"/>, <paramref name="evaluators"/>, <paramref name="messages"/>, or
    /// <paramref name="modelResponse"/> is <see langword="null"/>.
    /// </exception>
    public static Task<IReadOnlyList<EvaluationResult>> EvaluateAndRecordAsync(
        this ILangfuseScenario scenario,
        IEnumerable<IEvaluator> evaluators,
        IEnumerable<ChatMessage> messages,
        ChatResponse modelResponse) =>
        EvaluateAndRecordCoreAsync(
            scenario,
            evaluators,
            messages,
            modelResponse,
            chatConfiguration: null,
            additionalContext: null,
            scoreOptions: null,
            CancellationToken.None);

    /// <summary>
    /// Runs each evaluator over the supplied agent output and records every resulting metric as a
    /// Langfuse score on the scenario's trace using explicit evaluator, score, and cancellation
    /// configuration.
    /// </summary>
    /// <param name="scenario">The scenario whose trace the scores attach to.</param>
    /// <param name="evaluators">The evaluators to run.</param>
    /// <param name="messages">The conversation messages sent to the agent (for example, from <c>EvaluationInputs.Messages</c>).</param>
    /// <param name="modelResponse">The agent's response (for example, from <c>EvaluationInputs.ModelResponse</c>).</param>
    /// <param name="options">The evaluator execution and score projection configuration.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The evaluation results, in evaluator order.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="scenario"/>, <paramref name="evaluators"/>, <paramref name="messages"/>,
    /// <paramref name="modelResponse"/>, or <paramref name="options"/> is
    /// <see langword="null"/>.
    /// </exception>
    public static Task<IReadOnlyList<EvaluationResult>> EvaluateAndRecordAsync(
        this ILangfuseScenario scenario,
        IEnumerable<IEvaluator> evaluators,
        IEnumerable<ChatMessage> messages,
        ChatResponse modelResponse,
        LangfuseEvaluateAndRecordOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        return EvaluateAndRecordCoreAsync(
            scenario,
            evaluators,
            messages,
            modelResponse,
            options.ChatConfiguration,
            options.AdditionalContext,
            options.ScoreOptions,
            cancellationToken);
    }

    private static async Task<IReadOnlyList<EvaluationResult>> EvaluateAndRecordCoreAsync(
        ILangfuseScenario scenario,
        IEnumerable<IEvaluator> evaluators,
        IEnumerable<ChatMessage> messages,
        ChatResponse modelResponse,
        ChatConfiguration? chatConfiguration,
        IEnumerable<EvaluationContext>? additionalContext,
        LangfuseEvaluationScoreOptions? scoreOptions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(evaluators);
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(modelResponse);

        var materializedMessages = messages as IReadOnlyList<ChatMessage> ?? messages.ToList();
        var contextList = additionalContext?.ToList();
        var results = new List<EvaluationResult>();

        foreach (var evaluator in evaluators)
        {
            var result = await evaluator
                .EvaluateAsync(materializedMessages, modelResponse, chatConfiguration, contextList, cancellationToken)
                .ConfigureAwait(false);
            await scenario.RecordEvaluationAsync(result, scoreOptions, cancellationToken).ConfigureAwait(false);
            results.Add(result);
        }

        return results;
    }
}
