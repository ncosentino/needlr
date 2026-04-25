using Microsoft.Extensions.AI;

using NexusLabs.Needlr.AgentFramework.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Evaluation;

/// <summary>
/// Extensions that convert <see cref="IPipelineRunResult"/> into the input shape expected
/// by <c>Microsoft.Extensions.AI.Evaluation</c> evaluators.
/// </summary>
public static class PipelineEvaluationExtensions
{
    /// <summary>
    /// Projects a pipeline run result into <see cref="EvaluationInputs"/> for the full
    /// pipeline. Input messages are collected from all stages that have diagnostics, and
    /// the response is taken from the last stage that produced a non-null
    /// <see cref="ChatResponse"/>.
    /// </summary>
    /// <param name="result">The pipeline run result to convert.</param>
    /// <returns>
    /// An <see cref="EvaluationInputs"/> whose <see cref="EvaluationInputs.Messages"/> is
    /// the union of all stage input messages, and whose
    /// <see cref="EvaluationInputs.ModelResponse"/> is the last stage's response. When no
    /// stage produced a response, the returned response contains a single empty assistant
    /// message so evaluators always receive a non-null <see cref="ChatResponse"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> is <see langword="null"/>.</exception>
    public static EvaluationInputs ToEvaluationInputs(this IPipelineRunResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var allInputMessages = result.Stages
            .Where(s => s.Diagnostics is not null)
            .SelectMany(s => s.Diagnostics!.InputMessages)
            .ToList();

        var lastResponse = result.Stages
            .LastOrDefault(s => s.FinalResponse is not null)?.FinalResponse;
        var chatResponse = lastResponse
            ?? new ChatResponse(new ChatMessage(ChatRole.Assistant, string.Empty));

        return new EvaluationInputs(allInputMessages, chatResponse);
    }
}
