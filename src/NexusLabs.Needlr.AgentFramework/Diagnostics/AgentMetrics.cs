using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace NexusLabs.Needlr.AgentFramework.Diagnostics;

/// <summary>
/// Default <see cref="IAgentMetrics"/> implementation using <see cref="Meter"/> and
/// <see cref="ActivitySource"/> from <c>System.Diagnostics</c>. Compatible with
/// OpenTelemetry — metrics are exported when a listener is registered.
/// </summary>
/// <remarks>
/// Source names use <c>NexusLabs.Needlr.AgentFramework</c> — not hardcoded to any
/// consumer's namespace. Wire OpenTelemetry in the host to export these metrics.
/// </remarks>
internal sealed class AgentMetrics : IAgentMetrics
{
    internal const string MeterName = "NexusLabs.Needlr.AgentFramework";
    internal const string ActivitySourceName = "NexusLabs.Needlr.AgentFramework";

    internal static readonly ActivitySource Source = new(ActivitySourceName);

    private readonly Counter<long> _runsStarted;
    private readonly Counter<long> _runsCompleted;
    private readonly Histogram<double> _runDuration;
    private readonly Counter<long> _tokensUsed;
    private readonly Counter<long> _toolCallsCompleted;
    private readonly Histogram<double> _toolCallDuration;
    private readonly Histogram<double> _chatCompletionDuration;

    public AgentMetrics()
    {
        var meter = new Meter(MeterName);

        _runsStarted = meter.CreateCounter<long>(
            "agent.run.started",
            description: "Agent runs started");

        _runsCompleted = meter.CreateCounter<long>(
            "agent.run.completed",
            description: "Agent runs completed");

        _runDuration = meter.CreateHistogram<double>(
            "agent.run.duration",
            unit: "s",
            description: "Agent run execution duration");

        _tokensUsed = meter.CreateCounter<long>(
            "agent.tokens.used",
            description: "Tokens consumed by agent runs");

        _toolCallsCompleted = meter.CreateCounter<long>(
            "agent.tool.completed",
            description: "Agent tool calls completed");

        _toolCallDuration = meter.CreateHistogram<double>(
            "agent.tool.duration",
            unit: "s",
            description: "Agent tool call execution duration");

        _chatCompletionDuration = meter.CreateHistogram<double>(
            "agent.chat.duration",
            unit: "s",
            description: "Agent chat completion duration");
    }

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
    public void RecordToolCall(string toolName, TimeSpan duration, bool succeeded)
    {
        var status = succeeded ? "success" : "failed";
        KeyValuePair<string, object?> toolTag = new("tool_name", toolName);
        KeyValuePair<string, object?> statusTag = new("status", status);

        _toolCallsCompleted.Add(1, toolTag, statusTag);
        _toolCallDuration.Record(duration.TotalSeconds, toolTag, statusTag);
    }

    /// <inheritdoc />
    public void RecordChatCompletion(string model, TimeSpan duration, bool succeeded)
    {
        var status = succeeded ? "success" : "failed";
        _chatCompletionDuration.Record(
            duration.TotalSeconds,
            new KeyValuePair<string, object?>("model", model),
            new KeyValuePair<string, object?>("status", status));
    }
}
