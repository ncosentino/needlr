using Microsoft.Agents.AI;

namespace NexusLabs.Needlr.AgentFramework.Workflows.Sequential;

/// <summary>
/// Executes a pipeline stage by running an <see cref="AIAgent"/> with a
/// dynamically constructed prompt.
/// </summary>
/// <example>
/// <code>
/// var executor = new AgentStageExecutor(
///     writerAgent,
///     ctx => $"Write a draft about topic in workspace.");
///
/// var result = await executor.ExecuteAsync(context, cancellationToken);
/// Console.WriteLine(result.ResponseText);
/// </code>
/// </example>
[DoNotAutoRegister]
public sealed class AgentStageExecutor : IStageExecutor
{
    private readonly AIAgent _agent;
    private readonly Func<StageExecutionContext, string> _promptFactory;

    /// <summary>
    /// Initializes a new <see cref="AgentStageExecutor"/>.
    /// </summary>
    /// <param name="agent">The AI agent to execute.</param>
    /// <param name="promptFactory">
    /// Factory that produces the prompt string from the current stage context.
    /// </param>
    public AgentStageExecutor(
        AIAgent agent,
        Func<StageExecutionContext, string> promptFactory)
    {
        _agent = agent;
        _promptFactory = promptFactory;
    }

    /// <inheritdoc />
    public async Task<StageExecutionResult> ExecuteAsync(
        StageExecutionContext context,
        CancellationToken cancellationToken)
    {
        var prompt = _promptFactory(context);
        using (context.DiagnosticsAccessor.BeginCapture())
        {
            var response = await _agent.RunAsync(prompt, cancellationToken: cancellationToken);
            var diagnostics = context.DiagnosticsAccessor.LastRunDiagnostics;
            return StageExecutionResult.Success(context.StageName, diagnostics, response.GetText());
        }
    }
}
