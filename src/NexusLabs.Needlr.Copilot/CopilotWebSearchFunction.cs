using System.Text.Json;

using Microsoft.Extensions.AI;

namespace NexusLabs.Needlr.Copilot;

/// <summary>
/// <see cref="AIFunction"/> that calls the Copilot MCP server's <c>web_search</c> tool.
/// Register this with Needlr's agent framework tool set to give agents access to
/// Copilot's AI-powered web search.
/// </summary>
/// <example>
/// <code>
/// var tools = CopilotToolSet.Create(new CopilotToolSetOptions { EnableWebSearch = true });
/// // Pass tools to an agent via IterativeLoopOptions.Tools
/// </code>
/// </example>
public sealed class CopilotWebSearchFunction : AIFunction
{
    private readonly CopilotMcpToolClient _mcpClient;

    private static readonly JsonElement _schema = JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "query": {
                    "type": "string",
                    "description": "A clear, specific question or prompt that requires up-to-date information from the web."
                }
            },
            "required": ["query"]
        }
        """).RootElement.Clone();

    /// <summary>
    /// Creates a new <see cref="CopilotWebSearchFunction"/> backed by the given MCP client.
    /// </summary>
    /// <param name="mcpClient">The MCP client used to call the Copilot web search endpoint.</param>
    internal CopilotWebSearchFunction(CopilotMcpToolClient mcpClient)
    {
        _mcpClient = mcpClient ?? throw new ArgumentNullException(nameof(mcpClient));
    }

    /// <inheritdoc />
    public override string Name => "web_search";

    /// <inheritdoc />
    public override string Description =>
        "Performs an AI-powered web search to provide intelligent, contextual answers with citations. " +
        "Use when the query pertains to recent events, new developments, niche subjects, or when " +
        "current factual information with verifiable sources is needed.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _schema;

    /// <inheritdoc />
    protected override async ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        var query = arguments.TryGetValue("query", out var queryValue)
            ? queryValue?.ToString()
            : null;

        if (string.IsNullOrWhiteSpace(query))
        {
            return "Error: 'query' parameter is required.";
        }

        try
        {
            var result = await _mcpClient.CallToolAsync(
                "web_search",
                new Dictionary<string, string> { ["query"] = query },
                "web_search",
                cancellationToken).ConfigureAwait(false);

            return ExtractSearchText(result);
        }
        catch (Exception ex)
        {
            return $"Web search failed: {ex.Message}";
        }
    }

    private static string ExtractSearchText(string mcpResultText)
    {
        try
        {
            using var doc = JsonDocument.Parse(mcpResultText);
            var root = doc.RootElement;

            if (root.TryGetProperty("text", out var textObj) &&
                textObj.TryGetProperty("value", out var valueElement))
            {
                return valueElement.GetString() ?? mcpResultText;
            }

            if (root.TryGetProperty("text", out var directText) &&
                directText.ValueKind == JsonValueKind.String)
            {
                return directText.GetString() ?? mcpResultText;
            }
        }
        catch (JsonException)
        {
        }

        return mcpResultText;
    }
}
