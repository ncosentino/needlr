using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace NexusLabs.Needlr.AgentFramework.Diagnostics;

/// <summary>
/// Extensions for rendering an <see cref="IAgentRunDiagnostics"/> as a human-readable
/// markdown transcript.
/// </summary>
public static class AgentRunDiagnosticsTranscriptExtensions
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
    };

    /// <summary>
    /// Renders a deterministic markdown transcript of the agent run, including the
    /// input messages, ordered timeline of chat completions and tool calls, and the
    /// final output response. All numeric formatting uses <see cref="CultureInfo.InvariantCulture"/>.
    /// </summary>
    /// <param name="diagnostics">The agent run diagnostics to render.</param>
    /// <returns>A markdown string. Never <see langword="null"/>.</returns>
    public static string ToTranscriptMarkdown(
        this IAgentRunDiagnostics diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        var sb = new StringBuilder();
        var inv = CultureInfo.InvariantCulture;

        sb.Append("# Agent run: ").AppendLine(diagnostics.AgentName);
        sb.Append("- Execution mode: ").AppendLine(diagnostics.ExecutionMode ?? "(unspecified)");
        sb.Append("- Succeeded: ").AppendLine(diagnostics.Succeeded ? "true" : "false");
        sb.Append("- Total duration: ")
            .Append(((long)diagnostics.TotalDuration.TotalMilliseconds).ToString(inv))
            .AppendLine(" ms");
        var tokens = diagnostics.AggregateTokenUsage;
        sb.Append("- Aggregate tokens: input=")
            .Append(tokens.InputTokens.ToString(inv))
            .Append(", output=")
            .Append(tokens.OutputTokens.ToString(inv))
            .Append(", total=")
            .AppendLine(tokens.TotalTokens.ToString(inv));
        sb.AppendLine();

        if (diagnostics.InputMessages.Count > 0)
        {
            sb.AppendLine("## Input messages");
            sb.AppendLine();
            foreach (var message in diagnostics.InputMessages)
            {
                AppendChatMessage(sb, message);
            }
        }

        sb.AppendLine("## Timeline");
        sb.AppendLine();
        var timeline = diagnostics.GetOrderedTimeline();
        if (timeline.Count == 0)
        {
            sb.AppendLine("_No diagnostics captured._");
            sb.AppendLine();
        }
        else
        {
            foreach (var entry in timeline)
            {
                var offsetMs = (long)(entry.StartedAt - diagnostics.StartedAt).TotalMilliseconds;
                if (entry.Kind == DiagnosticsTimelineEntryKind.ChatCompletion
                    && entry.ChatCompletion is { } chat)
                {
                    AppendChat(sb, chat, offsetMs, inv);
                }
                else if (entry.Kind == DiagnosticsTimelineEntryKind.ToolCall
                    && entry.ToolCall is { } tool)
                {
                    AppendTool(sb, tool, offsetMs, inv);
                }
            }
        }

        if (diagnostics.OutputResponse is { } output && output.Messages.Count > 0)
        {
            sb.AppendLine("## Output response");
            sb.AppendLine();
            foreach (var message in output.Messages)
            {
                AppendChatMessage(sb, message);
            }
        }

        if (!diagnostics.Succeeded)
        {
            sb.AppendLine("## Error");
            sb.AppendLine();
            sb.AppendLine(diagnostics.ErrorMessage ?? "(no error message)");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static void AppendChat(
        StringBuilder sb,
        ChatCompletionDiagnostics chat,
        long offsetMs,
        CultureInfo inv)
    {
        sb.Append("### [+").Append(offsetMs.ToString(inv))
            .Append(" ms] Chat completion #").Append(chat.Sequence.ToString(inv)).AppendLine();
        sb.Append("- Model: ").AppendLine(chat.Model);
        sb.Append("- Duration: ")
            .Append(((long)chat.Duration.TotalMilliseconds).ToString(inv))
            .AppendLine(" ms");
        sb.Append("- Succeeded: ").AppendLine(chat.Succeeded ? "true" : "false");
        sb.Append("- Tokens: input=")
            .Append(chat.Tokens.InputTokens.ToString(inv))
            .Append(", output=")
            .Append(chat.Tokens.OutputTokens.ToString(inv))
            .Append(", total=")
            .AppendLine(chat.Tokens.TotalTokens.ToString(inv));
        sb.Append("- Request chars: ").AppendLine(chat.RequestCharCount.ToString(inv));
        sb.Append("- Response chars: ").AppendLine(chat.ResponseCharCount.ToString(inv));
        if (!chat.Succeeded && !string.IsNullOrEmpty(chat.ErrorMessage))
        {
            sb.Append("- Error: ").AppendLine(chat.ErrorMessage);
        }
        sb.AppendLine();
    }

    private static void AppendTool(
        StringBuilder sb,
        ToolCallDiagnostics tool,
        long offsetMs,
        CultureInfo inv)
    {
        sb.Append("### [+").Append(offsetMs.ToString(inv))
            .Append(" ms] Tool call #").Append(tool.Sequence.ToString(inv))
            .Append(": ").AppendLine(tool.ToolName);
        sb.Append("- Duration: ")
            .Append(((long)tool.Duration.TotalMilliseconds).ToString(inv))
            .AppendLine(" ms");
        sb.Append("- Succeeded: ").AppendLine(tool.Succeeded ? "true" : "false");
        sb.Append("- Arguments chars: ").AppendLine(tool.ArgumentsCharCount.ToString(inv));
        sb.Append("- Result chars: ").AppendLine(tool.ResultCharCount.ToString(inv));
        if (tool.Arguments is not null)
        {
            sb.AppendLine("- Arguments:");
            sb.AppendLine("```json");
            sb.AppendLine(SafeSerialize(tool.Arguments));
            sb.AppendLine("```");
        }
        if (tool.Result is not null)
        {
            sb.AppendLine("- Result:");
            sb.AppendLine("```json");
            sb.AppendLine(SafeSerialize(tool.Result));
            sb.AppendLine("```");
        }
        if (!tool.Succeeded && !string.IsNullOrEmpty(tool.ErrorMessage))
        {
            sb.Append("- Error: ").AppendLine(tool.ErrorMessage);
        }
        sb.AppendLine();
    }

    private static void AppendChatMessage(StringBuilder sb, ChatMessage message)
    {
        sb.Append("### ").AppendLine(message.Role.Value);
        var text = message.Text;
        if (!string.IsNullOrEmpty(text))
        {
            sb.AppendLine();
            sb.AppendLine(text);
        }
        sb.AppendLine();
    }

    private static string SafeSerialize(object value)
    {
        try
        {
            return JsonSerializer.Serialize(value, _jsonOptions);
        }
        catch (Exception ex)
        {
            return $"(serialization failed: {ex.GetType().Name}: {ex.Message})";
        }
    }
}
