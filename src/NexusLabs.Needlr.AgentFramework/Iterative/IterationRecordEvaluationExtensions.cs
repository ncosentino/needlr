using Microsoft.Extensions.AI;

namespace NexusLabs.Needlr.AgentFramework.Iterative;

/// <summary>
/// Extensions that convert <see cref="IterationRecord"/> and collections of iteration
/// records into Microsoft.Extensions.AI shapes suitable for evaluation libraries
/// (e.g., Microsoft.Extensions.AI.Evaluation's <c>ToolCallAccuracyEvaluator</c>).
/// </summary>
public static class IterationRecordEvaluationExtensions
{
    /// <summary>
    /// Materializes a tool-call trajectory as a list of <see cref="ChatMessage"/>
    /// instances suitable for Microsoft.Extensions.AI.Evaluation trajectory-aware
    /// evaluators. Each tool call becomes an assistant message containing a
    /// <see cref="FunctionCallContent"/>, immediately followed by a tool message
    /// containing the matching <see cref="FunctionResultContent"/>.
    /// </summary>
    /// <param name="record">The iteration record to convert.</param>
    /// <returns>
    /// An ordered trajectory of <see cref="ChatMessage"/> entries. Empty if the
    /// iteration produced no tool calls.
    /// </returns>
    public static IReadOnlyList<ChatMessage> ToToolCallTrajectory(this IterationRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (record.ToolCalls.Count == 0)
        {
            return Array.Empty<ChatMessage>();
        }

        var messages = new List<ChatMessage>(record.ToolCalls.Count * 2);
        for (var i = 0; i < record.ToolCalls.Count; i++)
        {
            var call = record.ToolCalls[i];
            var callId = $"i{record.Iteration}-c{i}";

            var callContent = new FunctionCallContent(
                callId: callId,
                name: call.FunctionName,
                arguments: call.Arguments is null
                    ? null
                    : new Dictionary<string, object?>(call.Arguments));
            messages.Add(new ChatMessage(ChatRole.Assistant, [callContent]));

            var resultContent = new FunctionResultContent(
                callId: callId,
                result: call.Succeeded
                    ? ToolResultSerializer.Serialize(call.Result)
                    : call.ErrorMessage);
            messages.Add(new ChatMessage(ChatRole.Tool, [resultContent]));
        }

        return messages;
    }

    /// <summary>
    /// Materializes a tool-call trajectory across an entire iterative run by
    /// concatenating the trajectories of each iteration in order.
    /// </summary>
    /// <param name="records">The iteration records to flatten.</param>
    /// <returns>
    /// An ordered trajectory of <see cref="ChatMessage"/> entries across all
    /// iterations. Empty if no iteration produced any tool calls.
    /// </returns>
    public static IReadOnlyList<ChatMessage> ToToolCallTrajectory(
        this IEnumerable<IterationRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        var messages = new List<ChatMessage>();
        foreach (var record in records)
        {
            foreach (var message in record.ToToolCallTrajectory())
            {
                messages.Add(message);
            }
        }

        return messages;
    }
}
