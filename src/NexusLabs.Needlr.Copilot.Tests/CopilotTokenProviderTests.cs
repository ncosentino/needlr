namespace NexusLabs.Needlr.Copilot.Tests;

public class CopilotTokenProviderTests
{
    [Fact]
    public void ReadFromEnvironment_ReturnsGhToken_WhenSet()
    {
        var origGh = Environment.GetEnvironmentVariable("GH_TOKEN");
        try
        {
            Environment.SetEnvironmentVariable("GH_TOKEN", "test-token-123");
            var result = GitHubOAuthTokenProvider.ReadFromEnvironment();
            Assert.Equal("test-token-123", result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GH_TOKEN", origGh);
        }
    }

    [Fact]
    public void ReadFromEnvironment_ReturnsGitHubToken_WhenGhTokenMissing()
    {
        var origGh = Environment.GetEnvironmentVariable("GH_TOKEN");
        var origGithub = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        try
        {
            Environment.SetEnvironmentVariable("GH_TOKEN", null);
            Environment.SetEnvironmentVariable("GITHUB_TOKEN", "github-token-456");
            var result = GitHubOAuthTokenProvider.ReadFromEnvironment();
            Assert.Equal("github-token-456", result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GH_TOKEN", origGh);
            Environment.SetEnvironmentVariable("GITHUB_TOKEN", origGithub);
        }
    }

    [Fact]
    public void GetAppsJsonPath_ReturnsNonNullOnSupportedPlatforms()
    {
        var path = GitHubOAuthTokenProvider.GetAppsJsonPath();
        Assert.NotNull(path);
        Assert.Contains("github-copilot", path);
        Assert.EndsWith("apps.json", path);
    }

    [Fact]
    public async Task GetTokenAsync_UsesExplicitToken_WhenProvided()
    {
        var options = new CopilotChatClientOptions { GitHubToken = "explicit-token" };

        var handler = new FakeHandler(_ =>
            new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"token": "copilot-api-token-xxx", "expires_at": 9999999999}""",
                    System.Text.Encoding.UTF8,
                    "application/json"),
            });

        using var httpClient = new HttpClient(handler);
        using var provider = new CopilotTokenProvider(options, httpClient);

        var token = await provider.GetTokenAsync(TestContext.Current.CancellationToken);

        Assert.Equal("copilot-api-token-xxx", token);
    }

    [Fact]
    public async Task GetTokenAsync_CachesToken_UntilExpiry()
    {
        int callCount = 0;
        var handler = new FakeHandler(_ =>
        {
            callCount++;
            var expiresAt = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"token\": \"token-" + callCount + "\", \"expires_at\": " + expiresAt + "}",
                    System.Text.Encoding.UTF8,
                    "application/json"),
            };
        });

        var options = new CopilotChatClientOptions
        {
            GitHubToken = "oauth-token",
            TokenRefreshBufferSeconds = 60,
        };

        using var httpClient = new HttpClient(handler);
        using var provider = new CopilotTokenProvider(options, httpClient);

        var first = await provider.GetTokenAsync(TestContext.Current.CancellationToken);
        var second = await provider.GetTokenAsync(TestContext.Current.CancellationToken);

        Assert.Equal(first, second);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task GetTokenAsync_ThrowsOnFailedExchange()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("bad credentials"),
        });

        var options = new CopilotChatClientOptions { GitHubToken = "bad-token" };
        using var httpClient = new HttpClient(handler);
        using var provider = new CopilotTokenProvider(options, httpClient);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => provider.GetTokenAsync(TestContext.Current.CancellationToken));
        Assert.Contains("Unauthorized", ex.Message);
    }

    [Fact]
    public void ExplicitToken_TakesPrecedenceOverTokenSource()
    {
        var options = new CopilotChatClientOptions
        {
            GitHubToken = "explicit-wins",
            TokenSource = CopilotTokenSource.EnvironmentVariable,
        };

        var provider = new GitHubOAuthTokenProvider(options);
        var token = provider.GetOAuthToken();

        Assert.Equal("explicit-wins", token);
    }

    [Fact]
    public void CopilotToolSet_ReturnsWebSearchFunction_WhenEnabled()
    {
        var options = new CopilotChatClientOptions { GitHubToken = "test" };
        var tools = CopilotToolSet.Create(
            new CopilotToolSetOptions { EnableWebSearch = true },
            options);

        Assert.Single(tools);
        Assert.Equal("web_search", tools[0].Name);
    }

    [Fact]
    public void CopilotToolSet_ReturnsEmpty_WhenNothingEnabled()
    {
        var tools = CopilotToolSet.Create(t => { }, new CopilotChatClientOptions { GitHubToken = "test" });
        Assert.Empty(tools);
    }

    [Fact]
    public void CopilotChatClientOptions_HasSensibleDefaults()
    {
        var options = new CopilotChatClientOptions();

        Assert.Equal("claude-sonnet-4.6", options.DefaultModel);
        Assert.Equal(CopilotTokenSource.Auto, options.TokenSource);
        Assert.Equal("https://api.githubcopilot.com", options.CopilotApiBaseUrl);
        Assert.Equal("https://api.github.com", options.GitHubApiBaseUrl);
        Assert.Equal("needlr-copilot", options.IntegrationId);
        Assert.Equal(3, options.MaxRetries);
        Assert.Equal(1000, options.RetryBaseDelayMs);
        Assert.Equal(60, options.TokenRefreshBufferSeconds);
    }

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(handler(request));
    }
}
