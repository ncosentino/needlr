using Microsoft.Agents.AI;

using NexusLabs.Needlr.AgentFramework.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Workflows.Sequential;

/// <summary>
/// Implements an evaluate→revise→retry loop. A critic agent evaluates the
/// current state, a programmatic check determines pass/fail, and if it fails
/// a reviser agent applies feedback. The loop repeats up to the configured
/// maximum number of additional attempts.
/// </summary>
/// <example>
/// <code>
/// var executor = new CritiqueAndReviseExecutor(
///     criticAgent,
///     reviserAgent,
///     ctx => $"Review the draft in the workspace for quality.",
///     (ctx, feedback) => $"Revise the draft. Feedback: {feedback}",
///     (diag, feedback) => feedback?.Contains("PASS") == true,
///     maxRetries: 2);
///
/// var result = await executor.ExecuteAsync(context, cancellationToken);
/// </code>
/// </example>
[DoNotAutoRegister]
public sealed class CritiqueAndReviseExecutor : IStageExecutor
{
    private readonly AIAgent _critic;
    private readonly AIAgent _reviser;
    private readonly Func<StageExecutionContext, string> _criticPromptFactory;
    private readonly Func<StageExecutionContext, string, string> _reviserPromptFactory;
    private readonly Func<IAgentRunDiagnostics?, string?, bool> _passCheck;
    private readonly int _maxRetries;

    /// <summary>
    /// Initializes a new <see cref="CritiqueAndReviseExecutor"/>.
    /// </summary>
    /// <param name="critic">The AI agent that evaluates the current state.</param>
    /// <param name="reviser">The AI agent that applies revision feedback.</param>
    /// <param name="criticPromptFactory">Builds the prompt sent to the critic agent from the stage context.</param>
    /// <param name="reviserPromptFactory">Builds the prompt sent to the reviser agent from the stage context and critic feedback.</param>
    /// <param name="passCheck">Determines whether the critic's response constitutes a pass.</param>
    /// <param name="maxRetries">Maximum number of revision attempts after the initial critique. Defaults to 3.</param>
    public CritiqueAndReviseExecutor(
        AIAgent critic,
        AIAgent reviser,
        Func<StageExecutionContext, string> criticPromptFactory,
        Func<StageExecutionContext, string, string> reviserPromptFactory,
        Func<IAgentRunDiagnostics?, string?, bool> passCheck,
        int maxRetries = 3)
    {
        _critic = critic;
        _reviser = reviser;
        _criticPromptFactory = criticPromptFactory;
        _reviserPromptFactory = reviserPromptFactory;
        _passCheck = passCheck;
        _maxRetries = maxRetries;
    }

    /// <inheritdoc />
    public async Task<StageExecutionResult> ExecuteAsync(
        StageExecutionContext context,
        CancellationToken cancellationToken)
    {
        var allDiagnostics = new List<IAgentRunDiagnostics>();
        string? feedback = null;
        bool passed = false;

        for (int i = 0; i <= _maxRetries; i++)
        {
            var criticPrompt = _criticPromptFactory(context);
            using (context.DiagnosticsAccessor.BeginCapture())
            {
                var response = await _critic.RunAsync(criticPrompt, cancellationToken: cancellationToken);
                feedback = response.GetText();
                var diag = context.DiagnosticsAccessor.LastRunDiagnostics;
                if (diag is not null)
                {
                    allDiagnostics.Add(diag);
                }

                passed = _passCheck(diag, feedback);
            }

            if (passed)
            {
                break;
            }

            // Run reviser only if not the last attempt
            if (i < _maxRetries)
            {
                var reviserPrompt = _reviserPromptFactory(context, feedback ?? "");
                using (context.DiagnosticsAccessor.BeginCapture())
                {
                    await _reviser.RunAsync(reviserPrompt, cancellationToken: cancellationToken);
                    var diag = context.DiagnosticsAccessor.LastRunDiagnostics;
                    if (diag is not null)
                    {
                        allDiagnostics.Add(diag);
                    }
                }
            }
        }

        var lastDiag = allDiagnostics.Count > 0 ? allDiagnostics[^1] : null;

        return passed
            ? StageExecutionResult.Success(context.StageName, lastDiag, feedback)
            : StageExecutionResult.Failed(
                context.StageName,
                new InvalidOperationException(
                    $"Critique-and-revise did not pass after {_maxRetries + 1} attempts. Last feedback: {feedback}"),
                lastDiag);
    }
}
