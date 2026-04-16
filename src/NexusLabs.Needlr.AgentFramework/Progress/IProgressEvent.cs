namespace NexusLabs.Needlr.AgentFramework.Progress;

/// <summary>
/// Base interface for all progress events emitted during agent/workflow execution.
/// Every event carries correlation context enabling hierarchical trace reconstruction.
/// </summary>
public interface IProgressEvent
{
    /// <summary>When the event occurred.</summary>
    DateTimeOffset Timestamp { get; }

    /// <summary>Top-level workflow correlation ID.</summary>
    string WorkflowId { get; }

    /// <summary>Which agent emitted this event, or <see langword="null"/> for workflow-level events.</summary>
    string? AgentId { get; }

    /// <summary>Parent agent ID for sub-agent runs, enabling tree reconstruction.</summary>
    string? ParentAgentId { get; }

    /// <summary>Nesting depth: 0 = workflow, 1 = agent, 2 = sub-agent, etc.</summary>
    int Depth { get; }

    /// <summary>Globally ordered sequence number for event ordering.</summary>
    long SequenceNumber { get; }
}

// ---------------------------------------------------------------------------
// Workflow-level events
// ---------------------------------------------------------------------------

/// <summary>Workflow execution has started.</summary>
public sealed record WorkflowStartedEvent(
    DateTimeOffset Timestamp,
    string WorkflowId,
    string? AgentId,
    string? ParentAgentId,
    int Depth,
    long SequenceNumber) : IProgressEvent;

/// <summary>Workflow execution has completed.</summary>
public sealed record WorkflowCompletedEvent(
    DateTimeOffset Timestamp,
    string WorkflowId,
    string? AgentId,
    string? ParentAgentId,
    int Depth,
    long SequenceNumber,
    bool Succeeded,
    string? ErrorMessage,
    TimeSpan TotalDuration) : IProgressEvent;

// ---------------------------------------------------------------------------
// Agent lifecycle events
// ---------------------------------------------------------------------------

/// <summary>An agent has been invoked (turn started).</summary>
public sealed record AgentInvokedEvent(
    DateTimeOffset Timestamp,
    string WorkflowId,
    string? AgentId,
    string? ParentAgentId,
    int Depth,
    long SequenceNumber,
    string AgentName) : IProgressEvent;

/// <summary>An agent has completed its turn.</summary>
public sealed record AgentCompletedEvent(
    DateTimeOffset Timestamp,
    string WorkflowId,
    string? AgentId,
    string? ParentAgentId,
    int Depth,
    long SequenceNumber,
    string AgentName,
    TimeSpan Duration,
    long TotalTokens,
    long? InputTokens = null,
    long? OutputTokens = null,
    int? ToolCallCount = null,
    string? TerminationReason = null) : IProgressEvent;

/// <summary>An agent run failed.</summary>
public sealed record AgentFailedEvent(
    DateTimeOffset Timestamp,
    string WorkflowId,
    string? AgentId,
    string? ParentAgentId,
    int Depth,
    long SequenceNumber,
    string AgentName,
    string ErrorMessage) : IProgressEvent;

/// <summary>An agent handed off to another agent.</summary>
public sealed record AgentHandoffEvent(
    DateTimeOffset Timestamp,
    string WorkflowId,
    string? AgentId,
    string? ParentAgentId,
    int Depth,
    long SequenceNumber,
    string FromAgentId,
    string ToAgentId) : IProgressEvent;

// ---------------------------------------------------------------------------
// LLM call events
// ---------------------------------------------------------------------------

/// <summary>An LLM request has been sent.</summary>
public sealed record LlmCallStartedEvent(
    DateTimeOffset Timestamp,
    string WorkflowId,
    string? AgentId,
    string? ParentAgentId,
    int Depth,
    long SequenceNumber,
    int CallSequence) : IProgressEvent;

/// <summary>An LLM response has been received.</summary>
public sealed record LlmCallCompletedEvent(
    DateTimeOffset Timestamp,
    string WorkflowId,
    string? AgentId,
    string? ParentAgentId,
    int Depth,
    long SequenceNumber,
    int CallSequence,
    string Model,
    TimeSpan Duration,
    long InputTokens,
    long OutputTokens,
    long TotalTokens) : IProgressEvent;

/// <summary>An LLM call failed.</summary>
public sealed record LlmCallFailedEvent(
    DateTimeOffset Timestamp,
    string WorkflowId,
    string? AgentId,
    string? ParentAgentId,
    int Depth,
    long SequenceNumber,
    int CallSequence,
    string ErrorMessage,
    TimeSpan Duration) : IProgressEvent;

// ---------------------------------------------------------------------------
// Tool call events
// ---------------------------------------------------------------------------

/// <summary>A tool invocation has started.</summary>
public sealed record ToolCallStartedEvent(
    DateTimeOffset Timestamp,
    string WorkflowId,
    string? AgentId,
    string? ParentAgentId,
    int Depth,
    long SequenceNumber,
    string ToolName) : IProgressEvent;

/// <summary>A tool invocation has completed.</summary>
public sealed record ToolCallCompletedEvent(
    DateTimeOffset Timestamp,
    string WorkflowId,
    string? AgentId,
    string? ParentAgentId,
    int Depth,
    long SequenceNumber,
    string ToolName,
    TimeSpan Duration,
    IReadOnlyDictionary<string, object?>? CustomMetrics) : IProgressEvent;

/// <summary>A tool invocation failed.</summary>
public sealed record ToolCallFailedEvent(
    DateTimeOffset Timestamp,
    string WorkflowId,
    string? AgentId,
    string? ParentAgentId,
    int Depth,
    long SequenceNumber,
    string ToolName,
    string ErrorMessage,
    TimeSpan Duration) : IProgressEvent;

// ---------------------------------------------------------------------------
// Budget events
// ---------------------------------------------------------------------------

/// <summary>Token budget usage has been updated.</summary>
public sealed record BudgetUpdatedEvent(
    DateTimeOffset Timestamp,
    string WorkflowId,
    string? AgentId,
    string? ParentAgentId,
    int Depth,
    long SequenceNumber,
    long CurrentInputTokens,
    long CurrentOutputTokens,
    long CurrentTotalTokens,
    long? MaxInputTokens,
    long? MaxOutputTokens,
    long? MaxTotalTokens) : IProgressEvent;

/// <summary>A token budget limit has been exceeded.</summary>
public sealed record BudgetExceededEvent(
    DateTimeOffset Timestamp,
    string WorkflowId,
    string? AgentId,
    string? ParentAgentId,
    int Depth,
    long SequenceNumber,
    string LimitType,
    long CurrentValue,
    long MaxValue) : IProgressEvent;

// ---------------------------------------------------------------------------
// Workflow control-flow events (MAF SuperSteps)
// ---------------------------------------------------------------------------

/// <summary>A workflow control-flow step (SuperStep) has started. Represents one
/// cycle of the orchestration loop: send work to agents → collect results → decide next.</summary>
public sealed record SuperStepStartedProgressEvent(
    DateTimeOffset Timestamp,
    string WorkflowId,
    string? AgentId,
    string? ParentAgentId,
    int Depth,
    long SequenceNumber,
    int StepNumber) : IProgressEvent;

/// <summary>A workflow control-flow step (SuperStep) has completed.</summary>
public sealed record SuperStepCompletedProgressEvent(
    DateTimeOffset Timestamp,
    string WorkflowId,
    string? AgentId,
    string? ParentAgentId,
    int Depth,
    long SequenceNumber,
    int StepNumber) : IProgressEvent;
