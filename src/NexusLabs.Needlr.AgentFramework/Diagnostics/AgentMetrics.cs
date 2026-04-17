using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace NexusLabs.Needlr.AgentFramework.Diagnostics;

/// <summary>
/// Default <see cref="IAgentMetrics"/> implementation using <see cref="Meter"/>
/// for counters/histograms and <see cref="System.Diagnostics.ActivitySource"/> for
/// distributed tracing spans. Compatible with OpenTelemetry — both metrics and traces
/// are exported when listeners are registered.
/// </summary>
/// <remarks>
/// Source names default to <c>"NexusLabs.Needlr.AgentFramework"</c> but can be
/// overridden via <see cref="AgentFrameworkMetricsOptions.MeterName"/> and
/// <see cref="AgentFrameworkMetricsOptions.ActivitySourceName"/> to match consumers'
/// existing dashboard queries.
/// </remarks>
[DoNotAutoRegister]
internal sealed class AgentMetrics : IAgentMetrics, IDisposable
{
    private readonly Meter _meter;
    private readonly ActivitySource _activitySource;
    private readonly Counter<long> _runsStarted;
    private readonly Counter<long> _runsCompleted;
    private readonly Histogram<double> _runDuration;
    private readonly Counter<long> _tokensUsed;
    private readonly Counter<long> _toolCallsCompleted;
    private readonly Histogram<double> _toolCallDuration;
    private readonly Histogram<double> _chatCompletionDuration;

    public AgentMetrics() : this(new AgentFrameworkMetricsOptions()) { }

    public AgentMetrics(AgentFrameworkMetricsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _meter = new Meter(options.MeterName);
        _activitySource = new ActivitySource(options.ResolvedActivitySourceName);

        _runsStarted = _meter.CreateCounter<long>(
            "agent.run.started",
            description: "Agent runs started");

        _runsCompleted = _meter.CreateCounter<long>(
            "agent.run.completed",
            description: "Agent runs completed");

        _runDuration = _meter.CreateHistogram<double>(
            "agent.run.duration",
            unit: "s",
            description: "Agent run execution duration");

        _tokensUsed = _meter.CreateCounter<long>(
            "agent.tokens.used",
            description: "Tokens consumed by agent runs");

        _toolCallsCompleted = _meter.CreateCounter<long>(
            "agent.tool.completed",
            description: "Agent tool calls completed");

        _toolCallDuration = _meter.CreateHistogram<double>(
            "agent.tool.duration",
            unit: "s",
            description: "Agent tool call execution duration");

        _chatCompletionDuration = _meter.CreateHistogram<double>(
            "agent.chat.duration",
            unit: "s",
            description: "Agent chat completion duration");
    }

    /// <inheritdoc />
    public ActivitySource ActivitySource => _activitySource;

    /// <inheritdoc />
    public void RecordRunStarted(string agentName) =>
        _runsStarted.Add(1, new KeyValuePair<string, object?>("agent_name", agentName));

    /// <inheritdoc />
    public void RecordRunCompleted(IAgentRunDiagnostics diagnostics)
    {
        var status = diagnostics.Succeeded ? "success" : "failed";
        KeyValuePair<string, object?> agentTag = new("agent_name", diagnostics.AgentName);
        KeyValuePair<string, object?> statusTag = new("status", status);

        _runsCompleted.Add(1, agentTag, statusTag);
        _runDuration.Record(diagnostics.TotalDuration.TotalSeconds, agentTag, statusTag);

        if (diagnostics.AggregateTokenUsage.InputTokens > 0)
        {
            _tokensUsed.Add(
                diagnostics.AggregateTokenUsage.InputTokens,
                agentTag, new KeyValuePair<string, object?>("direction", "input"));
        }

        if (diagnostics.AggregateTokenUsage.OutputTokens > 0)
        {
            _tokensUsed.Add(
                diagnostics.AggregateTokenUsage.OutputTokens,
                agentTag, new KeyValuePair<string, object?>("direction", "output"));
        }
    }

    /// <inheritdoc />
    public void RecordToolCall(string toolName, TimeSpan duration, bool succeeded, string? agentName = null)
    {
        var status = succeeded ? "success" : "failed";
        KeyValuePair<string, object?> toolTag = new("tool_name", toolName);
        KeyValuePair<string, object?> statusTag = new("status", status);

        if (agentName is not null)
        {
            KeyValuePair<string, object?> agentTag = new("agent_name", agentName);
            _toolCallsCompleted.Add(1, toolTag, statusTag, agentTag);
            _toolCallDuration.Record(duration.TotalSeconds, toolTag, statusTag, agentTag);
        }
        else
        {
            _toolCallsCompleted.Add(1, toolTag, statusTag);
            _toolCallDuration.Record(duration.TotalSeconds, toolTag, statusTag);
        }
    }

    /// <inheritdoc />
    public void RecordChatCompletion(string model, TimeSpan duration, bool succeeded, string? agentName = null)
    {
        var status = succeeded ? "success" : "failed";
        KeyValuePair<string, object?> modelTag = new("model", model);
        KeyValuePair<string, object?> statusTag = new("status", status);

        if (agentName is not null)
        {
            KeyValuePair<string, object?> agentTag = new("agent_name", agentName);
            _chatCompletionDuration.Record(duration.TotalSeconds, modelTag, statusTag, agentTag);
        }
        else
        {
            _chatCompletionDuration.Record(duration.TotalSeconds, modelTag, statusTag);
        }
    }

    /// <summary>Disposes the underlying <see cref="Meter"/> and <see cref="System.Diagnostics.ActivitySource"/>.</summary>
    public void Dispose()
    {
        _meter.Dispose();
        _activitySource.Dispose();
    }
}
