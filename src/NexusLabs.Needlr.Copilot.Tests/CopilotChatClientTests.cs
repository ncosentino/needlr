using System.Net;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.AI;

namespace NexusLabs.Needlr.Copilot.Tests;

public class CopilotChatClientTests
{
    private static readonly string TokenExchangeResponse =
        "{\"token\": \"copilot-test-token\", \"expires_at\": " +
        DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds() + "}";

    [Fact]
    public async Task GetResponseAsync_SendsOnlyProvidedTools_NeverInjectsExtras()
    {
        string? capturedBody = null;

        using var client = CreateClient(async req =>
        {
            if (req.RequestUri!.PathAndQuery.Contains("copilot_internal"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(TokenExchangeResponse, Encoding.UTF8, "application/json"),
                };
            }

            capturedBody = await req.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                        "id": "test-1",
                        "object": "chat.completion",
                        "created": 1700000000,
                        "model": "claude-sonnet-4",
                        "choices": [{
                            "index": 0,
                            "message": {"role": "assistant", "content": "Hello!"},
                            "finish_reason": "stop"
                        }],
                        "usage": {"prompt_tokens": 10, "completion_tokens": 5, "total_tokens": 15}
                    }
                    """,
                    Encoding.UTF8,
                    "application/json"),
            };
        });

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Hi"),
        };

        var response = await client.GetResponseAsync(messages, cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(capturedBody);
        using var doc = JsonDocument.Parse(capturedBody);
        var root = doc.RootElement;

        Assert.False(
            root.TryGetProperty("tools", out var toolsElement)
                && toolsElement.ValueKind == JsonValueKind.Array
                && toolsElement.GetArrayLength() > 0,
            "No tools should be sent when ChatOptions.Tools is empty.");
    }

    [Fact]
    public async Task GetResponseAsync_MapsResponseCorrectly()
    {
        using var client = CreateClient(req =>
        {
            if (req.RequestUri!.PathAndQuery.Contains("copilot_internal"))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(TokenExchangeResponse, Encoding.UTF8, "application/json"),
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                        "id": "resp-123",
                        "object": "chat.completion",
                        "created": 1700000000,
                        "model": "gpt-4o",
                        "choices": [{
                            "index": 0,
                            "message": {"role": "assistant", "content": "Hello world!"},
                            "finish_reason": "stop"
                        }],
                        "usage": {"prompt_tokens": 5, "completion_tokens": 3, "total_tokens": 8}
                    }
                    """,
                    Encoding.UTF8,
                    "application/json"),
            });
        });

        var response = await client.GetResponseAsync([new(ChatRole.User, "Hi")], cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("gpt-4o", response.ModelId);
        Assert.Equal(ChatFinishReason.Stop, response.FinishReason);
        Assert.NotNull(response.Usage);
        Assert.Equal(5, response.Usage!.InputTokenCount);
        Assert.Equal(3, response.Usage.OutputTokenCount);
        Assert.Equal(8, response.Usage.TotalTokenCount);

        var textContent = response.Messages
            .SelectMany(m => m.Contents.OfType<TextContent>())
            .FirstOrDefault();
        Assert.NotNull(textContent);
        Assert.Equal("Hello world!", textContent!.Text);
    }

    [Fact]
    public async Task GetResponseAsync_MapsToolCallsCorrectly()
    {
        using var client = CreateClient(req =>
        {
            if (req.RequestUri!.PathAndQuery.Contains("copilot_internal"))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(TokenExchangeResponse, Encoding.UTF8, "application/json"),
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                        "id": "resp-tc",
                        "object": "chat.completion",
                        "created": 1700000000,
                        "model": "claude-sonnet-4",
                        "choices": [{
                            "index": 0,
                            "message": {
                                "role": "assistant",
                                "content": null,
                                "tool_calls": [{
                                    "id": "call_abc123",
                                    "type": "function",
                                    "function": {
                                        "name": "get_weather",
                                        "arguments": "{\"location\": \"Seattle\"}"
                                    }
                                }]
                            },
                            "finish_reason": "tool_calls"
                        }]
                    }
                    """,
                    Encoding.UTF8,
                    "application/json"),
            });
        });

        var response = await client.GetResponseAsync([new(ChatRole.User, "What's the weather?")], cancellationToken: TestContext.Current.CancellationToken);;

        Assert.Equal(ChatFinishReason.ToolCalls, response.FinishReason);

        var funcCall = response.Messages
            .SelectMany(m => m.Contents.OfType<FunctionCallContent>())
            .FirstOrDefault();

        Assert.NotNull(funcCall);
        Assert.Equal("call_abc123", funcCall!.CallId);
        Assert.Equal("get_weather", funcCall.Name);
        Assert.NotNull(funcCall.Arguments);
        Assert.Equal("Seattle", funcCall.Arguments!["location"]?.ToString());
    }

    [Fact]
    public async Task GetResponseAsync_SendsRequiredHeaders()
    {
        HttpRequestMessage? capturedRequest = null;

        using var client = CreateClient(req =>
        {
            if (req.RequestUri!.PathAndQuery.Contains("copilot_internal"))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(TokenExchangeResponse, Encoding.UTF8, "application/json"),
                });
            }

            capturedRequest = req;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"id":"h","created":0,"model":"m","choices":[{"index":0,"message":{"role":"assistant","content":"ok"},"finish_reason":"stop"}]}""",
                    Encoding.UTF8,
                    "application/json"),
            });
        });

        await client.GetResponseAsync([new(ChatRole.User, "test")], cancellationToken: TestContext.Current.CancellationToken);;

        Assert.NotNull(capturedRequest);
        Assert.Equal("Bearer", capturedRequest!.Headers.Authorization?.Scheme);
        Assert.Equal("copilot-test-token", capturedRequest.Headers.Authorization?.Parameter);
        Assert.Contains("Copilot-Integration-Id", capturedRequest.Headers.Select(h => h.Key));
        Assert.Contains("Editor-Version", capturedRequest.Headers.Select(h => h.Key));
    }

    [Fact]
    public async Task GetStreamingResponseAsync_ParsesSSEChunks()
    {
        var sseData = new StringBuilder();
        sseData.AppendLine("data: {\"id\":\"s1\",\"created\":0,\"model\":\"m\",\"choices\":[{\"index\":0,\"delta\":{\"role\":\"assistant\",\"content\":\"Hel\"},\"finish_reason\":null}]}");
        sseData.AppendLine();
        sseData.AppendLine("data: {\"id\":\"s1\",\"created\":0,\"model\":\"m\",\"choices\":[{\"index\":0,\"delta\":{\"content\":\"lo!\"},\"finish_reason\":null}]}");
        sseData.AppendLine();
        sseData.AppendLine("data: {\"id\":\"s1\",\"created\":0,\"model\":\"m\",\"choices\":[{\"index\":0,\"delta\":{},\"finish_reason\":\"stop\"}]}");
        sseData.AppendLine();
        sseData.AppendLine("data: [DONE]");

        using var client = CreateClient(req =>
        {
            if (req.RequestUri!.PathAndQuery.Contains("copilot_internal"))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(TokenExchangeResponse, Encoding.UTF8, "application/json"),
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(sseData.ToString(), Encoding.UTF8, "text/event-stream"),
            });
        });

        var updates = new List<ChatResponseUpdate>();
        await foreach (var update in client.GetStreamingResponseAsync([new(ChatRole.User, "Hi")], cancellationToken: TestContext.Current.CancellationToken))
        {
            updates.Add(update);
        }

        var textParts = updates
            .SelectMany(u => u.Contents.OfType<TextContent>())
            .Select(t => t.Text)
            .ToList();

        Assert.Contains("Hel", textParts);
        Assert.Contains("lo!", textParts);
    }

    [Fact]
    public async Task GetStreamingResponseAsync_SkipsMalformedChunks()
    {
        var sseData = new StringBuilder();
        sseData.AppendLine("data: not-valid-json");
        sseData.AppendLine();
        sseData.AppendLine("data: {\"id\":\"s1\",\"created\":0,\"model\":\"m\",\"choices\":[{\"index\":0,\"delta\":{\"role\":\"assistant\",\"content\":\"ok\"},\"finish_reason\":\"stop\"}]}");
        sseData.AppendLine();
        sseData.AppendLine("data: [DONE]");

        using var client = CreateClient(req =>
        {
            if (req.RequestUri!.PathAndQuery.Contains("copilot_internal"))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(TokenExchangeResponse, Encoding.UTF8, "application/json"),
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(sseData.ToString(), Encoding.UTF8, "text/event-stream"),
            });
        });

        var updates = new List<ChatResponseUpdate>();
        await foreach (var update in client.GetStreamingResponseAsync([new(ChatRole.User, "Hi")], cancellationToken: TestContext.Current.CancellationToken))
        {
            updates.Add(update);
        }

        var textParts = updates.SelectMany(u => u.Contents.OfType<TextContent>()).ToList();
        Assert.Single(textParts);
        Assert.Equal("ok", textParts[0].Text);
    }

    [Fact]
    public void Metadata_ReturnsCorrectProviderName()
    {
        var options = new CopilotChatClientOptions { GitHubToken = "x" };
        using var client = new CopilotChatClient(options);

        Assert.Equal("github-copilot", client.Metadata.ProviderName);
    }

    [Fact]
    public async Task GetResponseAsync_ParallelToolResults_SendsAllResults()
    {
        var requestBodies = new List<string>();

        using var client = CreateClient(async req =>
        {
            if (req.RequestUri!.PathAndQuery.Contains("copilot_internal"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(TokenExchangeResponse, Encoding.UTF8, "application/json"),
                };
            }

            var body = await req.Content!.ReadAsStringAsync();
            requestBodies.Add(body);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                        "id": "test-par",
                        "object": "chat.completion",
                        "created": 1700000000,
                        "model": "claude-sonnet-4.6",
                        "choices": [{
                            "index": 0,
                            "message": {
                                "role": "assistant",
                                "content": "Done."
                            },
                            "finish_reason": "stop"
                        }]
                    }
                    """, Encoding.UTF8, "application/json"),
            };
        });

        // Simulate a conversation where the assistant made 3 parallel tool calls
        // and we're sending back 3 tool results. The Copilot API requires one
        // "tool" message per tool_call_id — packing them into one message violates
        // the Anthropic API contract.
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Search for 3 things"),
            new(ChatRole.Assistant,
            [
                new FunctionCallContent("call_1", "web_search", new Dictionary<string, object?> { ["query"] = "q1" }),
                new FunctionCallContent("call_2", "web_search", new Dictionary<string, object?> { ["query"] = "q2" }),
                new FunctionCallContent("call_3", "web_search", new Dictionary<string, object?> { ["query"] = "q3" }),
            ]),
            new(ChatRole.Tool,
            [
                new FunctionResultContent("call_1", "result 1"),
                new FunctionResultContent("call_2", "result 2"),
                new FunctionResultContent("call_3", "result 3"),
            ]),
        };

        await client.GetResponseAsync(messages, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(requestBodies);
        var json = JsonDocument.Parse(requestBodies[0]);
        var msgs = json.RootElement.GetProperty("messages");

        // Count tool-role messages — should be 3 (one per parallel tool result)
        var toolMessages = msgs.EnumerateArray()
            .Where(m => m.GetProperty("role").GetString() == "tool")
            .ToList();

        Assert.Equal(3, toolMessages.Count);
        Assert.Equal("call_1", toolMessages[0].GetProperty("tool_call_id").GetString());
        Assert.Equal("call_2", toolMessages[1].GetProperty("tool_call_id").GetString());
        Assert.Equal("call_3", toolMessages[2].GetProperty("tool_call_id").GetString());
        Assert.Equal("result 1", toolMessages[0].GetProperty("content").GetString());
        Assert.Equal("result 2", toolMessages[1].GetProperty("content").GetString());
        Assert.Equal("result 3", toolMessages[2].GetProperty("content").GetString());
    }

    private static CopilotChatClient CreateClient(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
    {
        var httpClient = new HttpClient(new DelegatingFakeHandler(handler));
        var options = new CopilotChatClientOptions { GitHubToken = "test-oauth-token" };
        return new CopilotChatClient(options, httpClient);
    }

    private sealed class DelegatingFakeHandler(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => handler(request);
    }
}
