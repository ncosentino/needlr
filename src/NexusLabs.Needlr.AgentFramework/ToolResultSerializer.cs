using System.Text.Json;

namespace NexusLabs.Needlr.AgentFramework;

/// <summary>
/// Shared utility for converting tool result objects to a string representation
/// suitable for display or inclusion in chat messages. Handles the <see cref="JsonElement"/>
/// values produced by MEAI's <c>AIFunctionFactory</c>, plain strings, and arbitrary objects.
/// </summary>
public static class ToolResultSerializer
{
    /// <summary>
    /// Serializes a tool result for inclusion in a <see cref="Microsoft.Extensions.AI.FunctionResultContent"/>
    /// or for display. <see cref="JsonElement"/> values are rendered to raw JSON text.
    /// Strings are returned as-is. Null returns empty string. All other types are
    /// JSON-serialized.
    /// </summary>
    public static string Serialize(object? result)
    {
        if (result is null)
        {
            return "";
        }

        if (result is JsonElement jsonElement)
        {
            return jsonElement.GetRawText();
        }

        if (result is string s)
        {
            return s;
        }

        try
        {
            return JsonSerializer.Serialize(result, result.GetType());
        }
        catch (JsonException)
        {
            return result.ToString() ?? "";
        }
    }
}
