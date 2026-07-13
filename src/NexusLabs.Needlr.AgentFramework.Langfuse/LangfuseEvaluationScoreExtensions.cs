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
    /// <see cref="ILangfuseScenario.RecordEvaluationAsync"/>, provided as a fluent call site for
    /// eval code that already holds an <see cref="EvaluationResult"/>.
    /// </summary>
    /// <param name="result">The evaluation result to project.</param>
    /// <param name="scenario">The scenario whose trace the scores attach to.</param>
    /// <param name="options">Optional stable identity settings for projected metric scores.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when Langfuse has accepted all projected scores.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="result"/> or <paramref name="scenario"/> is <see langword="null"/>.
    /// </exception>
    public static Task RecordLangfuseScoresAsync(
        this EvaluationResult result,
        ILangfuseScenario scenario,
        LangfuseEvaluationScoreOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(scenario);

        return scenario.RecordEvaluationAsync(result, options, cancellationToken);
    }

    /// <summary>
    /// Runs each evaluator over the supplied agent output and records every resulting metric as a
    /// Langfuse score on the scenario's trace. Collapses the per-test
    /// evaluate-then-record boilerplate into one call.
    /// </summary>
    /// <param name="scenario">The scenario whose trace the scores attach to.</param>
    /// <param name="evaluators">The evaluators to run.</param>
    /// <param name="messages">The conversation messages sent to the agent (for example, from <c>EvaluationInputs.Messages</c>).</param>
    /// <param name="modelResponse">The agent's response (for example, from <c>EvaluationInputs.ModelResponse</c>).</param>
    /// <param name="chatConfiguration">Optional chat configuration for LLM-judged evaluators.</param>
    /// <param name="additionalContext">Optional additional context (for example, <c>AgentRunDiagnosticsContext</c>).</param>
    /// <param name="scoreOptions">Optional stable identity settings for projected metric scores.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The evaluation results, in evaluator order.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="scenario"/>, <paramref name="evaluators"/>, <paramref name="messages"/>, or
    /// <paramref name="modelResponse"/> is <see langword="null"/>.
    /// </exception>
    public static async Task<IReadOnlyList<EvaluationResult>> EvaluateAndRecordAsync(
        this ILangfuseScenario scenario,
        IEnumerable<IEvaluator> evaluators,
        IEnumerable<ChatMessage> messages,
        ChatResponse modelResponse,
        ChatConfiguration? chatConfiguration = null,
        IEnumerable<EvaluationContext>? additionalContext = null,
        LangfuseEvaluationScoreOptions? scoreOptions = null,
        CancellationToken cancellationToken = default)
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
