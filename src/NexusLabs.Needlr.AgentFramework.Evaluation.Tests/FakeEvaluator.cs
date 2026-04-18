using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Tests;

internal sealed class FakeEvaluator : IEvaluator
{
    private readonly string _metricName;
    private readonly double _value;

    public FakeEvaluator(string metricName = "Score", double value = 1.0)
    {
        _metricName = metricName;
        _value = value;
    }

    public IReadOnlyCollection<string> EvaluationMetricNames => [_metricName];

    public int CallCount { get; private set; }
    public IEnumerable<ChatMessage>? LastMessages { get; private set; }
    public ChatResponse? LastModelResponse { get; private set; }
    public ChatConfiguration? LastChatConfiguration { get; private set; }

    public ValueTask<EvaluationResult> EvaluateAsync(
        IEnumerable<ChatMessage> messages,
        ChatResponse modelResponse,
        ChatConfiguration? chatConfiguration = null,
        IEnumerable<EvaluationContext>? additionalContext = null,
        CancellationToken cancellationToken = default)
    {
        CallCount++;
        LastMessages = messages;
        LastModelResponse = modelResponse;
        LastChatConfiguration = chatConfiguration;

        var result = new EvaluationResult(new NumericMetric(_metricName, _value, reason: null));
        return new ValueTask<EvaluationResult>(result);
    }
}
