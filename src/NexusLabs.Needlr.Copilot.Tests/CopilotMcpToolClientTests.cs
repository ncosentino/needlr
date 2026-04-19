using System.Net;

namespace NexusLabs.Needlr.Copilot.Tests;

public class CopilotMcpToolClientTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task CallToolAsync_429AfterRetryExhaustion_ThrowsCopilotRateLimitException()
    {
        var handler = new StubHandler(HttpStatusCode.TooManyRequests, "rate limited");
        using var httpClient = new HttpClient(handler);
        var options = new CopilotChatClientOptions { MaxRetries = 0, RetryBaseDelayMs = 1 };
        using var client = new CopilotMcpToolClient(
            new StubOAuthProvider(), options, httpClient);

        var ex = await Assert.ThrowsAsync<CopilotRateLimitException>(() =>
            client.CallToolAsync(
                "web_search",
                new Dictionary<string, string> { ["query"] = "test" },
                "web_search",
                _ct));

        Assert.Contains("rate limited", ex.Message);
    }

    [Fact]
    public async Task CallToolAsync_429AfterRetryExhaustion_PreservesRetryAfterHeader()
    {
        var handler = new StubHandler(
            HttpStatusCode.TooManyRequests,
            "rate limited",
            retryAfterSeconds: 42);
        using var httpClient = new HttpClient(handler);
        var options = new CopilotChatClientOptions { MaxRetries = 0, RetryBaseDelayMs = 1 };
        using var client = new CopilotMcpToolClient(
            new StubOAuthProvider(), options, httpClient);

        var ex = await Assert.ThrowsAsync<CopilotRateLimitException>(() =>
            client.CallToolAsync(
                "web_search",
                new Dictionary<string, string> { ["query"] = "test" },
                "web_search",
                _ct));

        Assert.Equal(TimeSpan.FromSeconds(42), ex.RetryAfter);
    }

    [Fact]
    public async Task CallToolAsync_429WithRetries_RetriesThenThrows()
    {
        var handler = new StubHandler(HttpStatusCode.TooManyRequests, "rate limited");
        using var httpClient = new HttpClient(handler);
        var options = new CopilotChatClientOptions { MaxRetries = 2, RetryBaseDelayMs = 1 };
        using var client = new CopilotMcpToolClient(
            new StubOAuthProvider(), options, httpClient);

        await Assert.ThrowsAsync<CopilotRateLimitException>(() =>
            client.CallToolAsync(
                "web_search",
                new Dictionary<string, string> { ["query"] = "test" },
                "web_search",
                _ct));

        Assert.Equal(3, handler.RequestCount);
    }

    [Fact]
    public async Task CallToolAsync_429ThenSuccess_ReturnsResult()
    {
        var sseBody = """
            event: message
            data: {"jsonrpc":"2.0","id":1,"result":{"content":[{"type":"text","text":"answer"}]}}
            """;
        var handler = new TransientFailureHandler(
            failCount: 1,
            failStatus: HttpStatusCode.TooManyRequests,
            successBody: sseBody);
        using var httpClient = new HttpClient(handler);
        var options = new CopilotChatClientOptions { MaxRetries = 3, RetryBaseDelayMs = 1 };
        using var client = new CopilotMcpToolClient(
            new StubOAuthProvider(), options, httpClient);

        var result = await client.CallToolAsync(
            "web_search",
            new Dictionary<string, string> { ["query"] = "test" },
            "web_search",
            _ct);

        Assert.Equal("answer", result);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task CallToolAsync_Non429Error_ThrowsHttpRequestException()
    {
        var handler = new StubHandler(HttpStatusCode.InternalServerError, "server error");
        using var httpClient = new HttpClient(handler);
        var options = new CopilotChatClientOptions { MaxRetries = 3, RetryBaseDelayMs = 1 };
        using var client = new CopilotMcpToolClient(
            new StubOAuthProvider(), options, httpClient);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.CallToolAsync(
                "web_search",
                new Dictionary<string, string> { ["query"] = "test" },
                "web_search",
                _ct));

        Assert.Contains("InternalServerError", ex.Message);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task CallToolAsync_Success_ReturnsToolContent()
    {
        var sseBody = """
            event: message
            data: {"jsonrpc":"2.0","id":1,"result":{"content":[{"type":"text","text":"Paris is the capital"}]}}
            """;
        var handler = new StubHandler(HttpStatusCode.OK, sseBody);
        using var httpClient = new HttpClient(handler);
        using var client = new CopilotMcpToolClient(
            new StubOAuthProvider(), httpClient: httpClient);

        var result = await client.CallToolAsync(
            "web_search",
            new Dictionary<string, string> { ["query"] = "capital of france" },
            "web_search",
            _ct);

        Assert.Equal("Paris is the capital", result);
    }

    private sealed class StubOAuthProvider : IGitHubOAuthTokenProvider
    {
        public string GetOAuthToken() => "fake-token";
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _body;
        private readonly int? _retryAfterSeconds;
        private int _requestCount;

        public StubHandler(
            HttpStatusCode statusCode,
            string body,
            int? retryAfterSeconds = null)
        {
            _statusCode = statusCode;
            _body = body;
            _retryAfterSeconds = retryAfterSeconds;
        }

        public int RequestCount => _requestCount;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _requestCount);

            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_body),
            };

            if (_retryAfterSeconds.HasValue)
            {
                response.Headers.RetryAfter =
                    new System.Net.Http.Headers.RetryConditionHeaderValue(
                        TimeSpan.FromSeconds(_retryAfterSeconds.Value));
            }

            return Task.FromResult(response);
        }
    }

    private sealed class TransientFailureHandler : HttpMessageHandler
    {
        private readonly int _failCount;
        private readonly HttpStatusCode _failStatus;
        private readonly string _successBody;
        private int _requestCount;

        public TransientFailureHandler(
            int failCount,
            HttpStatusCode failStatus,
            string successBody)
        {
            _failCount = failCount;
            _failStatus = failStatus;
            _successBody = successBody;
        }

        public int RequestCount => _requestCount;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var count = Interlocked.Increment(ref _requestCount);

            if (count <= _failCount)
            {
                return Task.FromResult(new HttpResponseMessage(_failStatus)
                {
                    Content = new StringContent("rate limited"),
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_successBody),
            });
        }
    }
}
