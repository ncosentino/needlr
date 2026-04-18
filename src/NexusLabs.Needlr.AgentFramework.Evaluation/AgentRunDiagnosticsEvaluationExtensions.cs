using Microsoft.Extensions.AI;
using NexusLabs.Needlr.AgentFramework.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Evaluation;

/// <summary>
/// Extensions that convert <see cref="IAgentRunDiagnostics"/> into the input shape expected
/// by <c>Microsoft.Extensions.AI.Evaluation</c> evaluators.
/// </summary>
public static class AgentRunDiagnosticsEvaluationExtensions
{
    /// <summary>
    /// Projects a captured agent run into <see cref="EvaluationInputs"/> that can be handed
    /// directly to any <c>IEvaluator.EvaluateAsync</c> overload without re-invoking the model.
    /// </summary>
    /// <param name="diagnostics">Diagnostics captured for a single agent run.</param>
    /// <returns>
    /// An <see cref="EvaluationInputs"/> whose <see cref="EvaluationInputs.Messages"/> is the
    /// captured input, and whose <see cref="EvaluationInputs.ModelResponse"/> is built from
    /// the captured <c>AgentResponse</c>. When the run produced no output response, the
    /// returned response contains a single empty assistant message so evaluators always
    /// receive a non-null <see cref="ChatResponse"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="diagnostics"/> is <see langword="null"/>.</exception>
    public static EvaluationInputs ToEvaluationInputs(this IAgentRunDiagnostics diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        var outputMessages = diagnostics.OutputResponse?.Messages;
        var chatResponse = outputMessages is { Count: > 0 }
            ? new ChatResponse(outputMessages)
            : new ChatResponse(new ChatMessage(ChatRole.Assistant, string.Empty));

        return new EvaluationInputs(diagnostics.InputMessages, chatResponse);
    }
}
