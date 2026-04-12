using Microsoft.Agents.AI;

namespace NexusLabs.Needlr.AgentFramework;

/// <summary>
/// Convenience extensions for <see cref="AgentResponse"/>.
/// </summary>
public static class AgentResponseExtensions
{
    /// <summary>
    /// Extracts the concatenated text content from all messages in the response.
    /// Returns <see langword="null"/> if the response has no messages or all messages
    /// have empty text.
    /// </summary>
    /// <remarks>
    /// <para>
    /// MAF's <see cref="AgentResponse.Messages"/> can contain multiple
    /// <see cref="Microsoft.Extensions.AI.ChatMessage"/> entries (e.g., tool-call results
    /// interleaved with assistant text). This method concatenates only the non-empty
    /// <c>Text</c> properties, separated by newlines, into a single string.
    /// </para>
    /// <para>
    /// Typical usage for standalone agent runs:
    /// <code>
    /// var response = await agent.RunAsync(prompt, cancellationToken: ct);
    /// var text = response.GetText(); // "The answer is 42."
    /// </code>
    /// </para>
    /// </remarks>
    public static string? GetText(this AgentResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (response.Messages is null or { Count: 0 })
            return null;

        string? result = null;
        foreach (var message in response.Messages)
        {
            if (string.IsNullOrWhiteSpace(message.Text))
                continue;

            result = result is null
                ? message.Text
                : string.Concat(result, "\n", message.Text);
        }

        return result;
    }
}
