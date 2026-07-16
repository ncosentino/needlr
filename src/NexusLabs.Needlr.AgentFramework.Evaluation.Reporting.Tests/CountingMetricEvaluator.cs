using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Reporting.Tests;

internal sealed class CountingMetricEvaluator : IEvaluator
{
    private readonly string _metricName;
    private int _callCount;

    public CountingMetricEvaluator(string metricName)
    {
        _metricName = metricName;
        EvaluationMetricNames = [metricName];
    }

    public int CallCount => Volatile.Read(ref _callCount);

    public IReadOnlyCollection<string> EvaluationMetricNames { get; }

    public ValueTask<EvaluationResult> EvaluateAsync(
        IEnumerable<ChatMessage> messages,
        ChatResponse modelResponse,
        ChatConfiguration? chatConfiguration = null,
        IEnumerable<EvaluationContext>? additionalContext = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var callCount = Interlocked.Increment(ref _callCount);
        return ValueTask.FromResult(new EvaluationResult(
            new NumericMetric(_metricName, callCount)));
    }
}
