using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace NexusLabs.Needlr.Copilot;

/// <summary>
/// Thin JSON-RPC client for calling tools on the GitHub Copilot MCP server
/// at <c>api.githubcopilot.com/mcp/readonly</c>. Parses SSE responses.
/// </summary>
internal sealed class CopilotMcpToolClient : IDisposable
{
    private readonly IGitHubOAuthTokenProvider _oauthProvider;
    private readonly CopilotChatClientOptions _options;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private int _nextId;

    public CopilotMcpToolClient(
        IGitHubOAuthTokenProvider oauthProvider,
        CopilotChatClientOptions? options = null,
        HttpClient? httpClient = null)
    {
        _oauthProvider = oauthProvider ?? throw new ArgumentNullException(nameof(oauthProvider));
        _options = options ?? new CopilotChatClientOptions();
        _ownsHttpClient = httpClient is null;
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<string> CallToolAsync(
        string toolName,
        Dictionary<string, string> arguments,
        string toolset,
        CancellationToken cancellationToken = default)
    {
        var requestId = Interlocked.Increment(ref _nextId);
        var rpcRequest = new McpJsonRpcRequest
        {
            Id = requestId,
            Method = "tools/call",
            Params = new McpCallParams
            {
                Name = toolName,
                Arguments = arguments,
            },
        };

        var oauthToken = _oauthProvider.GetOAuthToken();
        var url = $"{_options.CopilotApiBaseUrl.TrimEnd('/')}/mcp/readonly";

        var jsonBody = JsonSerializer.Serialize(rpcRequest, McpJsonContext.Default.McpJsonRpcRequest);

        for (int attempt = 0; ; attempt++)
        {
            var content = new StringContent(jsonBody, Encoding.UTF8);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = content,
            };

            httpRequest.Headers.Add("Authorization", $"Bearer {oauthToken}");
            httpRequest.Headers.Add("Accept", "application/json, text/event-stream");
            httpRequest.Headers.Add("X-MCP-Toolsets", toolset);
            httpRequest.Headers.Add("X-MCP-Host", "github-coding-agent");
            httpRequest.Headers.Add("Copilot-Integration-Id", _options.IntegrationId);

            using var httpResponse = await _httpClient.SendAsync(httpRequest, cancellationToken)
                .ConfigureAwait(false);

            if (httpResponse.IsSuccessStatusCode)
            {
                var responseText = await httpResponse.Content.ReadAsStringAsync(cancellationToken)
                    .ConfigureAwait(false);
                return ParseSseResponse(responseText);
            }

            if (httpResponse.StatusCode == System.Net.HttpStatusCode.TooManyRequests
                && attempt < _options.MaxRetries)
            {
                var delay = GetRetryDelay(httpResponse, attempt);
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                continue;
            }

            var errorBody = await httpResponse.Content.ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);
            throw new HttpRequestException(
                $"MCP tool call failed ({httpResponse.StatusCode}): {errorBody}");
        }
    }

    private TimeSpan GetRetryDelay(HttpResponseMessage response, int attempt)
    {
        if (response.Headers.RetryAfter?.Delta is { } delta)
            return delta;

        if (response.Headers.RetryAfter?.Date is { } date)
        {
            var wait = date - DateTimeOffset.UtcNow;
            if (wait > TimeSpan.Zero)
                return wait;
        }

        var ms = _options.RetryBaseDelayMs * (1 << attempt);
        return TimeSpan.FromMilliseconds(ms);
    }

    private static string ParseSseResponse(string sseText)
    {
        foreach (var line in sseText.Split('\n'))
        {
            if (!line.StartsWith("data: ", StringComparison.Ordinal))
            {
                continue;
            }

            var json = line["data: ".Length..];
            if (json is "[DONE]")
            {
                continue;
            }

            McpJsonRpcResponse? rpcResponse;
            try
            {
                rpcResponse = JsonSerializer.Deserialize(json, McpJsonContext.Default.McpJsonRpcResponse);
            }
            catch (JsonException)
            {
                continue;
            }

            if (rpcResponse?.Error is { } error)
            {
                throw new InvalidOperationException(
                    $"MCP tool returned error ({error.Code}): {error.Message}");
            }

            if (rpcResponse?.Result?.Content is { Count: > 0 } contents)
            {
                var textParts = contents
                    .Where(c => !string.IsNullOrEmpty(c.Text))
                    .Select(c => c.Text!);
                return string.Join("\n", textParts);
            }
        }

        throw new InvalidOperationException("MCP response contained no data.");
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }
}
