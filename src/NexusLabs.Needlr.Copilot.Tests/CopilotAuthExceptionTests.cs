using System.Net;

namespace NexusLabs.Needlr.Copilot.Tests;

/// <summary>
/// Tests that authentication failures across the Copilot stack surface as
/// a typed <see cref="CopilotAuthException"/> rather than HttpRequestException,
/// InvalidOperationException, or - worst of all - a fake-success WebSearchResult
/// containing the auth-failure prose as its Text. Mirrors the rate-limit
/// detection contract exposed via <see cref="CopilotRateLimitException"/>.
/// </summary>
public sealed class CopilotAuthExceptionTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public void GitHubOAuthTokenProvider_AutoSource_NoTokenFound_ThrowsCopilotAuthException()
    {
        var origGh = Environment.GetEnvironmentVariable("GH_TOKEN");
        var origGithub = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        try
        {
            Environment.SetEnvironmentVariable("GH_TOKEN", null);
            Environment.SetEnvironmentVariable("GITHUB_TOKEN", null);

            var options = new CopilotChatClientOptions
            {
                GitHubToken = null,
                TokenSource = CopilotTokenSource.EnvironmentVariable,
            };
            var provider = new GitHubOAuthTokenProvider(options);

            var ex = Assert.Throws<CopilotAuthException>(() => provider.GetOAuthToken());
            Assert.Contains("GH_TOKEN", ex.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GH_TOKEN", origGh);
            Environment.SetEnvironmentVariable("GITHUB_TOKEN", origGithub);
        }
    }

    [Fact]
    public async Task CopilotMcpToolClient_Http401_ThrowsCopilotAuthException()
    {
        var handler = new Http401Handler();
        using var httpClient = new HttpClient(handler);
        using var client = new CopilotMcpToolClient(
            new FakeOAuthProvider(), httpClient: httpClient);

        var ex = await Assert.ThrowsAsync<CopilotAuthException>(() =>
            client.CallToolAsync(
                "web_search",
                new Dictionary<string, string> { ["query"] = "test" },
                "web_search",
                _ct));

        Assert.Contains("401", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CopilotMcpToolClient_Http403_ThrowsCopilotAuthException()
    {
        var handler = new Http403Handler();
        using var httpClient = new HttpClient(handler);
        using var client = new CopilotMcpToolClient(
            new FakeOAuthProvider(), httpClient: httpClient);

        await Assert.ThrowsAsync<CopilotAuthException>(() =>
            client.CallToolAsync(
                "web_search",
                new Dictionary<string, string> { ["query"] = "test" },
                "web_search",
                _ct));
    }

    [Fact]
    public async Task CopilotWebSearchFunction_TokenProviderThrowsAuth_PropagatesAsCopilotAuthException()
    {
        using var httpClient = new HttpClient(new NeverInvokedHandler());
        var mcpClient = new CopilotMcpToolClient(
            new ThrowingOAuthProvider(), httpClient: httpClient);
        var fn = new CopilotWebSearchFunction(mcpClient);

        await Assert.ThrowsAsync<CopilotAuthException>(() => fn.InvokeAsync(
            new Microsoft.Extensions.AI.AIFunctionArguments { ["query"] = "anything" },
            _ct).AsTask());
    }

    [Fact]
    public async Task CopilotWebSearchFunction_McpReturns401_PropagatesAsCopilotAuthException()
    {
        var handler = new Http401Handler();
        using var httpClient = new HttpClient(handler);
        var mcpClient = new CopilotMcpToolClient(
            new FakeOAuthProvider(), httpClient: httpClient);
        var fn = new CopilotWebSearchFunction(mcpClient);

        await Assert.ThrowsAsync<CopilotAuthException>(() => fn.InvokeAsync(
            new Microsoft.Extensions.AI.AIFunctionArguments { ["query"] = "anything" },
            _ct).AsTask());
    }

    private sealed class FakeOAuthProvider : IGitHubOAuthTokenProvider
    {
        public string GetOAuthToken() => "fake-token";
    }

    private sealed class ThrowingOAuthProvider : IGitHubOAuthTokenProvider
    {
        public string GetOAuthToken() =>
            throw new CopilotAuthException(
                "No GitHub OAuth token found. Log in via the Copilot CLI, " +
                "set GH_TOKEN/GITHUB_TOKEN, or provide an explicit GitHubToken in options.");
    }

    private sealed class Http401Handler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("unauthorized"),
            });
        }
    }

    private sealed class Http403Handler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = new StringContent("forbidden"),
            });
        }
    }

    private sealed class NeverInvokedHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException(
                "HTTP handler should not be invoked when token provider throws.");
        }
    }
}
