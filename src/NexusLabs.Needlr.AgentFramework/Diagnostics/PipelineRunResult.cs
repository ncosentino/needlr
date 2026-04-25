using Microsoft.Extensions.AI;

namespace NexusLabs.Needlr.AgentFramework.Diagnostics;

/// <summary>
/// Default implementation of <see cref="IPipelineRunResult"/>.
/// </summary>
[DoNotAutoRegister]
internal sealed class PipelineRunResult : IPipelineRunResult
{
    private readonly Lazy<IReadOnlyDictionary<string, ChatResponse?>> _lazyResponses;
    private readonly Lazy<TokenUsage?> _lazyAggregateTokenUsage;

    internal PipelineRunResult(
        IReadOnlyList<IAgentStageResult> stages,
        TimeSpan totalDuration,
        bool succeeded,
        string? errorMessage,
        Exception? exception = null,
        int? plannedStageCount = null)
    {
        Stages = stages;
        PlannedStageCount = plannedStageCount ?? stages.Count;
        TotalDuration = totalDuration;
        Succeeded = succeeded;
        ErrorMessage = errorMessage;
        Exception = exception;

        // GroupBy handles duplicate agent names (last stage wins).
        _lazyResponses = new Lazy<IReadOnlyDictionary<string, ChatResponse?>>(() =>
            stages
                .GroupBy(s => s.AgentName)
                .ToDictionary(g => g.Key, g => g.Last().FinalResponse));

        _lazyAggregateTokenUsage = new Lazy<TokenUsage?>(() =>
        {
            var diagnostics = stages
                .Select(s => s.Diagnostics)
                .Where(d => d is not null)
                .ToList();

            if (diagnostics.Count == 0) return null;

            // AggregateTokenUsage is always non-null on IAgentRunDiagnostics produced by
            // AgentRunDiagnosticsBuilder.Build() — the ! suppression is safe.
            return new TokenUsage(
                InputTokens: diagnostics.Sum(d => d!.AggregateTokenUsage.InputTokens),
                OutputTokens: diagnostics.Sum(d => d!.AggregateTokenUsage.OutputTokens),
                TotalTokens: diagnostics.Sum(d => d!.AggregateTokenUsage.TotalTokens),
                CachedInputTokens: diagnostics.Sum(d => d!.AggregateTokenUsage.CachedInputTokens),
                ReasoningTokens: diagnostics.Sum(d => d!.AggregateTokenUsage.ReasoningTokens));
        });
    }

    public IReadOnlyList<IAgentStageResult> Stages { get; }

    public int PlannedStageCount { get; }

    public IReadOnlyDictionary<string, ChatResponse?> FinalResponses => _lazyResponses.Value;

    public TimeSpan TotalDuration { get; }

    public TokenUsage? AggregateTokenUsage => _lazyAggregateTokenUsage.Value;

    public bool Succeeded { get; }

    public string? ErrorMessage { get; }

    public Exception? Exception { get; }
}
