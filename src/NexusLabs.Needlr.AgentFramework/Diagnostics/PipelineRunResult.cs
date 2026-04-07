namespace NexusLabs.Needlr.AgentFramework.Diagnostics;

/// <summary>
/// Default implementation of <see cref="IPipelineRunResult"/>.
/// </summary>
[DoNotAutoRegister]
internal sealed class PipelineRunResult : IPipelineRunResult
{
    private readonly Lazy<IReadOnlyDictionary<string, string>> _lazyResponses;
    private readonly Lazy<TokenUsage?> _lazyAggregateTokenUsage;

    internal PipelineRunResult(
        IReadOnlyList<IAgentStageResult> stages,
        TimeSpan totalDuration,
        bool succeeded,
        string? errorMessage)
    {
        Stages = stages;
        TotalDuration = totalDuration;
        Succeeded = succeeded;
        ErrorMessage = errorMessage;

        _lazyResponses = new Lazy<IReadOnlyDictionary<string, string>>(() =>
            stages.ToDictionary(s => s.AgentName, s => s.ResponseText));

        _lazyAggregateTokenUsage = new Lazy<TokenUsage?>(() =>
        {
            var diagnostics = stages
                .Select(s => s.Diagnostics)
                .Where(d => d is not null)
                .ToList();

            if (diagnostics.Count == 0) return null;

            return new TokenUsage(
                InputTokens: diagnostics.Sum(d => d!.AggregateTokenUsage.InputTokens),
                OutputTokens: diagnostics.Sum(d => d!.AggregateTokenUsage.OutputTokens),
                TotalTokens: diagnostics.Sum(d => d!.AggregateTokenUsage.TotalTokens),
                CachedInputTokens: diagnostics.Sum(d => d!.AggregateTokenUsage.CachedInputTokens),
                ReasoningTokens: diagnostics.Sum(d => d!.AggregateTokenUsage.ReasoningTokens));
        });
    }

    public IReadOnlyList<IAgentStageResult> Stages { get; }

    public IReadOnlyDictionary<string, string> Responses => _lazyResponses.Value;

    public TimeSpan TotalDuration { get; }

    public TokenUsage? AggregateTokenUsage => _lazyAggregateTokenUsage.Value;

    public bool Succeeded { get; }

    public string? ErrorMessage { get; }
}
