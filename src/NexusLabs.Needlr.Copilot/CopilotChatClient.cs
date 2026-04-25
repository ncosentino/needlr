using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.AI;

namespace NexusLabs.Needlr.Copilot;

/// <summary>
/// <see cref="IChatClient"/> implementation that calls the GitHub Copilot chat completions
/// API directly (raw model endpoint, no CLI agent harness). Only tools explicitly provided
/// via <see cref="ChatOptions.Tools"/> are sent; no built-in Copilot CLI tools are injected.
/// </summary>
/// <remarks>
/// <para>
/// Authentication follows a two-step flow: a GitHub OAuth token (discovered from
/// <c>apps.json</c>, environment variables, or an explicit value) is exchanged for a
/// short-lived Copilot API bearer token via the internal GitHub API endpoint.
/// </para>
/// <para>
/// Plug this into Needlr's agent framework via
/// <c>.UsingChatClient(new CopilotChatClient())</c> — no Copilot-specific
/// syringe extensions required.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Minimal usage — auto-discovers token from Copilot CLI login:
/// IChatClient client = new CopilotChatClient();
///
/// // With explicit model and token:
/// IChatClient client = new CopilotChatClient(new CopilotChatClientOptions
/// {
///     DefaultModel = "gpt-5.4",
///     GitHubToken = "gho_xxx",
/// });
/// </code>
/// </example>
public sealed class CopilotChatClient : IChatClient
{
    private readonly ICopilotTokenProvider _tokenProvider;
    private readonly HttpClient _httpClient;
    private readonly CopilotChatClientOptions _options;
    private readonly bool _ownsHttpClient;

    /// <summary>
    /// Creates a new <see cref="CopilotChatClient"/> with optional configuration and HTTP client.
    /// Token discovery uses <see cref="CopilotChatClientOptions.TokenSource"/>.
    /// </summary>
    /// <param name="options">Configuration options. Uses defaults when <c>null</c>.</param>
    /// <param name="httpClient">Optional HTTP client (shared with token provider). Created internally if <c>null</c>.</param>
    public CopilotChatClient(CopilotChatClientOptions? options = null, HttpClient? httpClient = null)
        : this(
            new CopilotTokenProvider(options ?? new CopilotChatClientOptions(), httpClient),
            options,
            httpClient)
    {
    }

    /// <summary>
    /// Creates a new <see cref="CopilotChatClient"/> with a custom token provider.
    /// </summary>
    /// <param name="tokenProvider">Supplies Copilot API bearer tokens.</param>
    /// <param name="options">Configuration options. Uses defaults when <c>null</c>.</param>
    /// <param name="httpClient">Optional HTTP client. Created internally if <c>null</c>.</param>
    public CopilotChatClient(
        ICopilotTokenProvider tokenProvider,
        CopilotChatClientOptions? options = null,
        HttpClient? httpClient = null)
    {
        _tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
        _options = options ?? new CopilotChatClientOptions();
        _ownsHttpClient = httpClient is null;
        _httpClient = httpClient ?? new HttpClient();
    }

    /// <inheritdoc />
    public ChatClientMetadata Metadata => new("github-copilot");

    /// <inheritdoc />
    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var messageList = chatMessages as IList<ChatMessage> ?? chatMessages.ToList();
        var request = BuildRequest(messageList, options, stream: false);
        using var httpResponse = await SendRequestAsync(request, cancellationToken).ConfigureAwait(false);

        var body = await httpResponse.Content
            .ReadAsStringAsync(cancellationToken)
            .ConfigureAwait(false);

        var response = JsonSerializer.Deserialize(body, CopilotJsonContext.Default.ChatCompletionResponse)
            ?? throw new InvalidOperationException("Copilot API returned null response.");

        return MapToChatResponse(response, options);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var messageList = chatMessages as IList<ChatMessage> ?? chatMessages.ToList();
        var request = BuildRequest(messageList, options, stream: true);
        using var httpResponse = await SendRequestAsync(request, cancellationToken).ConfigureAwait(false);

        using var stream = await httpResponse.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);

            if (line is null)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(line) || line.StartsWith(':'))
            {
                continue;
            }

            if (!line.StartsWith("data: ", StringComparison.Ordinal))
            {
                continue;
            }

            var data = line["data: ".Length..];

            if (data is "[DONE]")
            {
                break;
            }

            ChatCompletionChunk? chunk;
            try
            {
                chunk = JsonSerializer.Deserialize(data, CopilotJsonContext.Default.ChatCompletionChunk);
            }
            catch (JsonException)
            {
                continue;
            }

            if (chunk is null)
            {
                continue;
            }

            var update = MapToStreamingUpdate(chunk);
            if (update is not null)
            {
                yield return update;
            }
        }
    }

    /// <inheritdoc />
    public object? GetService(Type serviceType, object? key = null)
    {
        if (serviceType == typeof(IChatClient))
        {
            return this;
        }

        return null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }

        if (_tokenProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private ChatCompletionRequest BuildRequest(
        IList<ChatMessage> messages,
        ChatOptions? options,
        bool stream)
    {
        var model = options?.ModelId ?? _options.DefaultModel;

        var request = new ChatCompletionRequest
        {
            Model = model,
            Messages = MapMessages(messages),
            Stream = stream,
            Temperature = options?.Temperature,
            TopP = options?.TopP,
            MaxTokens = options?.MaxOutputTokens,
            FrequencyPenalty = options?.FrequencyPenalty,
            PresencePenalty = options?.PresencePenalty,
        };

        if (options?.StopSequences is { Count: > 0 } stops)
        {
            request.Stop = [.. stops];
        }

        if (options?.Tools is { Count: > 0 } tools)
        {
            request.Tools = MapTools(tools);
        }

        return request;
    }

    private static List<RequestMessage> MapMessages(IList<ChatMessage> messages)
    {
        var result = new List<RequestMessage>(messages.Count);

        foreach (var msg in messages)
        {
            var role = msg.Role.Value switch
            {
                "system" => "system",
                "user" => "user",
                "assistant" => "assistant",
                "tool" => "tool",
                _ => msg.Role.Value,
            };

            // Handle tool result messages — one RequestMessage per FunctionResultContent.
            // MAF may pack multiple tool results into a single ChatMessage when the model
            // made parallel tool calls. The Copilot API (OpenAI format) requires a separate
            // "tool" message for each tool_call_id.
            var functionResults = msg.Contents.OfType<FunctionResultContent>().ToList();
            if (functionResults.Count > 0)
            {
                foreach (var fr in functionResults)
                {
                    result.Add(new RequestMessage
                    {
                        Role = role,
                        Content = SerializeToolResult(fr.Result),
                        ToolCallId = fr.CallId ?? "",
                    });
                }

                continue;
            }

            // Handle assistant messages with tool calls
            var functionCalls = msg.Contents.OfType<FunctionCallContent>()
                .Where(fc => !string.IsNullOrEmpty(fc.Name))
                .ToList();
            if (functionCalls.Count > 0)
            {
                var textContent = string.Join("", msg.Contents
                    .OfType<TextContent>()
                    .Select(t => t.Text));

                result.Add(new RequestMessage
                {
                    Role = role,
                    Content = string.IsNullOrEmpty(textContent) ? null : textContent,
                    ToolCalls = functionCalls.Select(fc => new RequestToolCall
                    {
                        Id = fc.CallId ?? "",
                        Type = "function",
                        Function = new RequestToolCallFunction
                        {
                            Name = fc.Name ?? "",
                            Arguments = fc.Arguments is not null
                                ? JsonSerializer.Serialize(fc.Arguments, CopilotJsonContext.Default.Options)
                                : "{}",
                        },
                    }).ToList(),
                });
                continue;
            }

            // Regular text message
            var text = string.Join("", msg.Contents
                .OfType<TextContent>()
                .Select(t => t.Text));

            result.Add(new RequestMessage
            {
                Role = role,
                Content = text,
            });
        }

        return result;
    }

    private static List<RequestTool> MapTools(IList<AITool> tools)
    {
        var result = new List<RequestTool>(tools.Count);

        foreach (var tool in tools)
        {
            if (tool is not AIFunction func)
            {
                continue;
            }

            var parameters = func.JsonSchema is { } schema
                ? JsonSerializer.Deserialize<object>(schema.GetRawText())
                : null;

            result.Add(new RequestTool
            {
                Type = "function",
                Function = new RequestToolFunction
                {
                    Name = func.Name,
                    Description = func.Description,
                    Parameters = parameters,
                },
            });
        }

        return result;
    }

    private async Task<HttpResponseMessage> SendRequestAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken)
    {
        var token = await _tokenProvider.GetTokenAsync(cancellationToken).ConfigureAwait(false);
        var url = $"{_options.CopilotApiBaseUrl.TrimEnd('/')}/chat/completions";

        for (int attempt = 0; ; attempt++)
        {
            var jsonBody = JsonSerializer.Serialize(
                request, CopilotJsonContext.Default.ChatCompletionRequest);

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(jsonBody, Encoding.UTF8, "application/json"),
            };

            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            httpRequest.Headers.Add("Accept", request.Stream ? "text/event-stream" : "application/json");
            httpRequest.Headers.Add("Copilot-Integration-Id", _options.IntegrationId);
            httpRequest.Headers.Add("Editor-Version", _options.EditorVersion);
            httpRequest.Headers.Add("Editor-Plugin-Version", "needlr-copilot/1.0.0");
            httpRequest.Headers.Add("X-GitHub-Api-Version", "2025-05-01");
            httpRequest.Headers.Add("Openai-Intent", "conversation-agent");
            httpRequest.Headers.Add("X-Interaction-Type", "conversation-agent");
            httpRequest.Headers.Add("X-Initiator", "user");
            httpRequest.Headers.UserAgent.ParseAdd(_options.IntegrationId);

            var httpResponse = await _httpClient.SendAsync(
                httpRequest,
                request.Stream ? HttpCompletionOption.ResponseHeadersRead : HttpCompletionOption.ResponseContentRead,
                cancellationToken).ConfigureAwait(false);

            if (httpResponse.IsSuccessStatusCode)
            {
                return httpResponse;
            }

            if (httpResponse.StatusCode == System.Net.HttpStatusCode.TooManyRequests
                && attempt < _options.MaxRetries)
            {
                var delay = GetRetryDelay(httpResponse, attempt);
                httpResponse.Dispose();
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                continue;
            }

            var errorBody = await httpResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            httpResponse.Dispose();
            throw new HttpRequestException(
                $"Copilot API request failed ({httpResponse.StatusCode}): {errorBody}");
        }
    }

    private TimeSpan GetRetryDelay(HttpResponseMessage response, int attempt)
    {
        if (response.Headers.RetryAfter?.Delta is { } delta)
        {
            return delta;
        }

        if (response.Headers.RetryAfter?.Date is { } date)
        {
            var wait = date - DateTimeOffset.UtcNow;
            if (wait > TimeSpan.Zero)
            {
                return wait;
            }
        }

        var ms = _options.RetryBaseDelayMs * (1 << attempt);
        return TimeSpan.FromMilliseconds(ms);
    }

    private ChatResponse MapToChatResponse(ChatCompletionResponse response, ChatOptions? options)
    {
        var messages = new List<ChatMessage>();

        foreach (var choice in response.Choices)
        {
            var msg = choice.Message;
            if (msg is null) continue;

            var chatMsg = new ChatMessage(
                new ChatRole(msg.Role ?? "assistant"),
                []);

            if (!string.IsNullOrEmpty(msg.Content))
            {
                chatMsg.Contents.Add(new TextContent(msg.Content));
            }

            if (msg.ToolCalls is { Count: > 0 })
            {
                foreach (var tc in msg.ToolCalls)
                {
                    if (tc.Function is null) continue;

                    IDictionary<string, object?>? args = null;
                    if (!string.IsNullOrEmpty(tc.Function.Arguments))
                    {
                        try
                        {
                            args = JsonSerializer.Deserialize<Dictionary<string, object?>>(
                                tc.Function.Arguments,
                                CopilotJsonContext.Default.Options);
                        }
                        catch (JsonException)
                        {
                            args = new Dictionary<string, object?> { ["_raw"] = tc.Function.Arguments };
                        }
                    }

                    chatMsg.Contents.Add(new FunctionCallContent(
                        tc.Id ?? "",
                        tc.Function.Name ?? "",
                        args));
                }
            }

            messages.Add(chatMsg);
        }

        var chatResponse = new ChatResponse(messages)
        {
            ModelId = response.Model ?? options?.ModelId ?? _options.DefaultModel,
            FinishReason = MapFinishReason(response.Choices.FirstOrDefault()?.FinishReason),
            CreatedAt = DateTimeOffset.FromUnixTimeSeconds(response.Created),
        };

        if (response.Id is not null)
        {
            chatResponse.AdditionalProperties ??= new AdditionalPropertiesDictionary();
            chatResponse.AdditionalProperties["completion_id"] = response.Id;
        }

        if (response.Usage is { } usage)
        {
            chatResponse.Usage = new UsageDetails
            {
                InputTokenCount = usage.PromptTokens,
                OutputTokenCount = usage.CompletionTokens,
                TotalTokenCount = usage.TotalTokens,
            };
        }

        return chatResponse;
    }

    private static ChatResponseUpdate? MapToStreamingUpdate(ChatCompletionChunk chunk)
    {
        var choice = chunk.Choices.FirstOrDefault();
        var delta = choice?.Delta;

        if (delta is null && chunk.Usage is null)
        {
            return null;
        }

        var update = new ChatResponseUpdate
        {
            ModelId = chunk.Model,
            CreatedAt = chunk.Created > 0 ? DateTimeOffset.FromUnixTimeSeconds(chunk.Created) : null,
            FinishReason = MapFinishReason(choice?.FinishReason),
            Role = delta?.Role is not null ? new ChatRole(delta.Role) : null,
        };

        if (chunk.Id is not null)
        {
            update.AdditionalProperties ??= new AdditionalPropertiesDictionary();
            update.AdditionalProperties["completion_id"] = chunk.Id;
        }

        if (!string.IsNullOrEmpty(delta?.Content))
        {
            update.Contents.Add(new TextContent(delta!.Content));
        }

        if (delta?.ToolCalls is { Count: > 0 })
        {
            foreach (var tc in delta.ToolCalls)
            {
                if (tc.Function is null) continue;

                IDictionary<string, object?>? args = null;
                if (!string.IsNullOrEmpty(tc.Function.Arguments))
                {
                    try
                    {
                        args = JsonSerializer.Deserialize<Dictionary<string, object?>>(
                            tc.Function.Arguments,
                            CopilotJsonContext.Default.Options);
                    }
                    catch (JsonException)
                    {
                        args = new Dictionary<string, object?> { ["_raw"] = tc.Function.Arguments };
                    }
                }

                update.Contents.Add(new FunctionCallContent(
                    tc.Id ?? "",
                    tc.Function.Name ?? "",
                    args));
            }
        }

        if (chunk.Usage is { } usage)
        {
            update.Contents.Add(new UsageContent(new UsageDetails
            {
                InputTokenCount = usage.PromptTokens,
                OutputTokenCount = usage.CompletionTokens,
                TotalTokenCount = usage.TotalTokens,
            }));
        }

        return update;
    }

    private static ChatFinishReason? MapFinishReason(string? reason) => reason switch
    {
        "stop" => ChatFinishReason.Stop,
        "length" => ChatFinishReason.Length,
        "tool_calls" => ChatFinishReason.ToolCalls,
        "content_filter" => ChatFinishReason.ContentFilter,
        null => null,
        _ => new ChatFinishReason(reason),
    };

    /// <summary>
    /// Serializes a tool result for inclusion in a chat message to the API.
    /// <see cref="JsonElement"/> values are rendered to raw JSON text. Strings
    /// are returned as-is. All other types are JSON-serialized.
    /// </summary>
    private static string SerializeToolResult(object? result)
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
