using System.Text.Json;

using Microsoft.Extensions.AI;

namespace NexusLabs.Needlr.Copilot;

/// <summary>
/// <see cref="AIFunction"/> that calls the Copilot MCP server's <c>web_search</c> tool.
/// Returns a <see cref="WebSearchResult"/> containing the answer text, source
/// citations, and the search queries that were performed.
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

            var searchResult = ParseSearchResult(result);
            ThrowIfRateLimited(searchResult);
            return searchResult;
        }
        catch (CopilotRateLimitException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return $"Web search failed: {ex.Message}";
        }
    }

    internal static void ThrowIfRateLimited(WebSearchResult result)
    {
        if (result.Text.StartsWith("Rate limit exceeded", StringComparison.OrdinalIgnoreCase) ||
            result.Text.StartsWith("Too many requests", StringComparison.OrdinalIgnoreCase))
        {
            // Only trigger for genuine rate-limit responses: these will have
            // no citations and no search queries. A real search result that
            // discusses rate limiting will have structured citation data.
            if (result.Citations.Count == 0 && result.SearchQueries.Count == 0)
            {
                var retryAfter = CopilotRateLimitException.ParseRetryAfterFromText(result.Text);
                throw new CopilotRateLimitException(
                    $"Copilot web search rate limited: {result.Text}",
                    retryAfter);
            }
        }
    }

    internal static WebSearchResult ParseSearchResult(string mcpResultText)
    {
        string text = mcpResultText;
        List<WebSearchCitation>? citations = null;
        List<WebSearchQuery>? searchQueries = null;

        try
        {
            using var doc = JsonDocument.Parse(mcpResultText);
            var root = doc.RootElement;

            // Extract the answer text from text.value (preferred) or text as a
            // direct string (fallback).
            if (root.TryGetProperty("text", out var textObj))
            {
                if (textObj.ValueKind == JsonValueKind.Object &&
                    textObj.TryGetProperty("value", out var valueElement))
                {
                    text = valueElement.GetString() ?? mcpResultText;

                    // Parse text.annotations (citation data)
                    citations = ParseCitations(textObj);
                }
                else if (textObj.ValueKind == JsonValueKind.String)
                {
                    text = textObj.GetString() ?? mcpResultText;
                }
            }

            // Fall back to root-level annotations if text.annotations was
            // absent or empty, in case the API shape evolves.
            if ((citations is null || citations.Count == 0) &&
                root.TryGetProperty("annotations", out var rootAnnotations))
            {
                citations = ParseCitations(rootAnnotations);
            }

            // Parse bing_searches
            if (root.TryGetProperty("bing_searches", out var bingSearches) &&
                bingSearches.ValueKind == JsonValueKind.Array)
            {
                searchQueries = ParseSearchQueries(bingSearches);
            }
        }
        catch (JsonException)
        {
            // Best-effort: return whatever we have so far.
        }

        return new WebSearchResult(
            text,
            citations?.AsReadOnly() ?? (IReadOnlyList<WebSearchCitation>)Array.Empty<WebSearchCitation>(),
            searchQueries?.AsReadOnly() ?? (IReadOnlyList<WebSearchQuery>)Array.Empty<WebSearchQuery>());
    }

    private static List<WebSearchCitation>? ParseCitations(JsonElement parent)
    {
        JsonElement annotations;
        if (parent.ValueKind == JsonValueKind.Object)
        {
            if (!parent.TryGetProperty("annotations", out annotations))
            {
                return null;
            }
        }
        else if (parent.ValueKind == JsonValueKind.Array)
        {
            annotations = parent;
        }
        else
        {
            return null;
        }

        if (annotations.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var citations = new List<WebSearchCitation>();
        foreach (var ann in annotations.EnumerateArray())
        {
            try
            {
                if (!ann.TryGetProperty("url_citation", out var urlCitation))
                {
                    continue;
                }

                var title = urlCitation.TryGetProperty("title", out var t)
                    ? t.GetString() ?? ""
                    : "";
                var url = urlCitation.TryGetProperty("url", out var u)
                    ? u.GetString() ?? ""
                    : "";
                var startIndex = ann.TryGetProperty("start_index", out var si)
                    && si.ValueKind == JsonValueKind.Number
                    && si.TryGetInt32(out var siVal)
                    ? siVal
                    : 0;
                var endIndex = ann.TryGetProperty("end_index", out var ei)
                    && ei.ValueKind == JsonValueKind.Number
                    && ei.TryGetInt32(out var eiVal)
                    ? eiVal
                    : 0;

                citations.Add(new WebSearchCitation(title, url, startIndex, endIndex));
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException)
            {
                // Skip malformed individual citations.
            }
        }

        return citations;
    }

    private static List<WebSearchQuery>? ParseSearchQueries(JsonElement bingSearches)
    {
        if (bingSearches.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var queries = new List<WebSearchQuery>();
        foreach (var item in bingSearches.EnumerateArray())
        {
            try
            {
                var queryText = item.TryGetProperty("text", out var t)
                    ? t.GetString() ?? ""
                    : "";
                var url = item.TryGetProperty("url", out var u)
                    ? u.GetString() ?? ""
                    : "";

                queries.Add(new WebSearchQuery(queryText, url));
            }
            catch (JsonException)
            {
                // Skip malformed individual search entries.
            }
        }

        return queries;
    }
}
