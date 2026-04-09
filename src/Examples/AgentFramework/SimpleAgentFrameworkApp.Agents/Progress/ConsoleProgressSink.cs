using NexusLabs.Needlr.AgentFramework.Progress;

namespace SimpleAgentFrameworkApp.Agents;

/// <summary>
/// Prints progress events to the console in real-time as they occur.
/// Demonstrates the <see cref="IProgressSink"/> pattern.
/// </summary>
public sealed class ConsoleProgressSink : IProgressSink
{
    public ValueTask OnEventAsync(IProgressEvent evt, CancellationToken cancellationToken)
    {
        var prefix = new string(' ', evt.Depth * 2);

        switch (evt)
        {
            case WorkflowStartedEvent:
                Console.WriteLine($"{prefix}[PROGRESS] Workflow started (id: {evt.WorkflowId})");
                break;

            case WorkflowCompletedEvent wc:
                Console.WriteLine($"{prefix}[PROGRESS] Workflow completed — {(wc.Succeeded ? "OK" : $"FAILED: {wc.ErrorMessage}")} ({wc.TotalDuration.TotalMilliseconds:F0}ms)");
                break;

            case AgentInvokedEvent ai:
                Console.WriteLine($"{prefix}[PROGRESS]   Agent invoked: {ai.AgentName}");
                break;

            case AgentCompletedEvent ac:
                Console.WriteLine($"{prefix}[PROGRESS]   Agent completed: {ac.AgentName} ({ac.Duration.TotalMilliseconds:F0}ms, {ac.TotalTokens} tokens)");
                break;

            case AgentHandoffEvent ah:
                Console.WriteLine($"{prefix}[PROGRESS]   Handoff: {ah.FromAgentId} → {ah.ToAgentId}");
                break;

            case LlmCallStartedEvent lcs:
                Console.WriteLine($"{prefix}[PROGRESS]     LLM call #{lcs.CallSequence} started...");
                break;

            case LlmCallCompletedEvent lcc:
                Console.WriteLine($"{prefix}[PROGRESS]     LLM call #{lcc.CallSequence} completed: {lcc.Duration.TotalMilliseconds:F0}ms model={lcc.Model} tokens={lcc.TotalTokens}");
                break;

            case LlmCallFailedEvent lcf:
                Console.WriteLine($"{prefix}[PROGRESS]     LLM call #{lcf.CallSequence} FAILED: {lcf.ErrorMessage} ({lcf.Duration.TotalMilliseconds:F0}ms)");
                break;

            case ToolCallStartedEvent tcs:
                Console.WriteLine($"{prefix}[PROGRESS]     Tool {tcs.ToolName} started...");
                break;

            case ToolCallCompletedEvent tcc:
                Console.WriteLine($"{prefix}[PROGRESS]     Tool {tcc.ToolName} completed: {tcc.Duration.TotalMilliseconds:F0}ms");
                break;

            case BudgetUpdatedEvent bu:
                Console.WriteLine($"{prefix}[PROGRESS]   Budget: {bu.CurrentTotalTokens}/{bu.MaxTotalTokens ?? 0} total, {bu.CurrentInputTokens}/{bu.MaxInputTokens ?? 0} in, {bu.CurrentOutputTokens}/{bu.MaxOutputTokens ?? 0} out");
                break;

            case BudgetExceededEvent be:
                Console.WriteLine($"{prefix}[PROGRESS]   BUDGET EXCEEDED: {be.LimitType} {be.CurrentValue}/{be.MaxValue}");
                break;
        }

        return ValueTask.CompletedTask;
    }
}
