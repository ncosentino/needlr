using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;

using NexusLabs.Needlr.AgentFramework.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Evaluation;

/// <summary>
/// Deterministic evaluator that scores the token efficiency and cost profile of an
/// agent run from the captured <see cref="IAgentRunDiagnostics"/> snapshot carried in
/// an <see cref="AgentRunDiagnosticsContext"/>.
/// </summary>
/// <remarks>
/// <para>
/// This evaluator never contacts a language model. It reads
/// <see cref="IAgentRunDiagnostics.AggregateTokenUsage"/> and
/// <see cref="IAgentRunDiagnostics.ToolCalls"/> to produce:
/// </para>
/// <list type="bullet">
///   <item><description><c>Total Tokens</c> — aggregate token count across all LLM calls.</description></item>
///   <item><description><c>Input Token Ratio</c> — input tokens / total tokens. High values suggest verbose prompts; low values suggest verbose outputs.</description></item>
///   <item><description><c>Tokens Per Tool Call</c> — total tokens / tool call count. Measures the token cost of each tool invocation.</description></item>
///   <item><description><c>Cache Hit Ratio</c> — cached input tokens / input tokens. Higher values mean more prompt-cache reuse.</description></item>
///   <item><description><c>Under Budget</c> — boolean. <see langword="true"/> when total tokens is strictly below the configured token budget. Only emitted when a budget is configured.</description></item>
/// </list>
/// <para>
/// When no <see cref="AgentRunDiagnosticsContext"/> is present in the
/// <c>additionalContext</c> collection, the evaluator returns an empty
/// <see cref="EvaluationResult"/> — callers should treat that as "not applicable".
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Score efficiency with a 10,000-token budget
/// var evaluator = new EfficiencyEvaluator(tokenBudget: 10_000);
/// var result = await evaluator.EvaluateAsync(
///     messages: Array.Empty&lt;ChatMessage&gt;(),
///     modelResponse: new ChatResponse(),
///     additionalContext: [new AgentRunDiagnosticsContext(diagnostics)]);
///
/// var underBudget = ((BooleanMetric)result.Metrics["Under Budget"]).Value;
/// var tokensPerTool = ((NumericMetric)result.Metrics["Tokens Per Tool Call"]).Value;
/// </code>
/// </example>
public sealed class EfficiencyEvaluator : IEvaluator
{
    /// <summary>Metric name for the aggregate token count.</summary>
    public const string TotalTokensMetricName = "Total Tokens";

    /// <summary>Metric name for the input-to-total token ratio.</summary>
    public const string InputTokenRatioMetricName = "Input Token Ratio";

    /// <summary>Metric name for tokens consumed per tool call.</summary>
    public const string TokensPerToolCallMetricName = "Tokens Per Tool Call";

    /// <summary>Metric name for the prompt-cache hit ratio.</summary>
    public const string CacheHitRatioMetricName = "Cache Hit Ratio";

    /// <summary>Metric name for the boolean budget check.</summary>
    public const string UnderBudgetMetricName = "Under Budget";

    private readonly long? _tokenBudget;

    /// <summary>
    /// Creates a new <see cref="EfficiencyEvaluator"/>.
    /// </summary>
    /// <param name="tokenBudget">
    /// Optional token budget. When provided, the evaluator emits the
    /// <see cref="UnderBudgetMetricName"/> metric. When <see langword="null"/>,
    /// the metric is omitted.
    /// </param>
    public EfficiencyEvaluator(long? tokenBudget = null)
    {
        _tokenBudget = tokenBudget;

        var names = new List<string>
        {
            TotalTokensMetricName,
            InputTokenRatioMetricName,
            TokensPerToolCallMetricName,
            CacheHitRatioMetricName,
        };
        if (tokenBudget.HasValue)
        {
            names.Add(UnderBudgetMetricName);
        }
        EvaluationMetricNames = names;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<string> EvaluationMetricNames { get; }

    /// <inheritdoc />
    public ValueTask<EvaluationResult> EvaluateAsync(
        IEnumerable<ChatMessage> messages,
        ChatResponse modelResponse,
        ChatConfiguration? chatConfiguration = null,
        IEnumerable<EvaluationContext>? additionalContext = null,
        CancellationToken cancellationToken = default)
    {
        var diagnostics = additionalContext?
            .OfType<AgentRunDiagnosticsContext>()
            .FirstOrDefault()?
            .Diagnostics;

        if (diagnostics is null)
        {
            return new ValueTask<EvaluationResult>(new EvaluationResult());
        }

        var usage = diagnostics.AggregateTokenUsage;
        var totalTokens = usage.TotalTokens;
        var inputTokens = usage.InputTokens;
        var cachedInputTokens = usage.CachedInputTokens;
        var toolCallCount = diagnostics.ToolCalls.Count;

        var inputTokenRatio = totalTokens > 0
            ? (double)inputTokens / totalTokens
            : 0;

        var tokensPerToolCall = toolCallCount > 0
            ? (double)totalTokens / toolCallCount
            : 0;

        var cacheHitRatio = inputTokens > 0
            ? (double)cachedInputTokens / inputTokens
            : 0;

        var metrics = new List<EvaluationMetric>
        {
            new NumericMetric(
                TotalTokensMetricName,
                value: totalTokens,
                reason: totalTokens == 0
                    ? "No token usage was recorded."
                    : $"{totalTokens:N0} total tokens consumed ({inputTokens:N0} input, {usage.OutputTokens:N0} output)."),

            new NumericMetric(
                InputTokenRatioMetricName,
                value: inputTokenRatio,
                reason: totalTokens == 0
                    ? "No tokens to compute ratio."
                    : $"{inputTokenRatio:P1} of tokens were input ({inputTokens:N0} of {totalTokens:N0})."),

            new NumericMetric(
                TokensPerToolCallMetricName,
                value: tokensPerToolCall,
                reason: toolCallCount == 0
                    ? "No tool calls to compute per-call cost."
                    : $"{tokensPerToolCall:N0} tokens per tool call ({totalTokens:N0} tokens / {toolCallCount} calls)."),

            new NumericMetric(
                CacheHitRatioMetricName,
                value: cacheHitRatio,
                reason: inputTokens == 0
                    ? "No input tokens to compute cache ratio."
                    : $"{cacheHitRatio:P1} of input tokens were cache hits ({cachedInputTokens:N0} of {inputTokens:N0})."),
        };

        if (_tokenBudget.HasValue)
        {
            var underBudget = totalTokens < _tokenBudget.Value;
            metrics.Add(new BooleanMetric(
                UnderBudgetMetricName,
                value: underBudget,
                reason: underBudget
                    ? $"Token usage ({totalTokens:N0}) is under the budget of {_tokenBudget.Value:N0}."
                    : $"Token usage ({totalTokens:N0}) reached or exceeded the budget of {_tokenBudget.Value:N0}."));
        }

        return new ValueTask<EvaluationResult>(new EvaluationResult(metrics.ToArray()));
    }
}
