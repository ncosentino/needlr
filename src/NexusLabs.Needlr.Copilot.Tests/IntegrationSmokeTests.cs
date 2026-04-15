using Microsoft.Extensions.AI;

namespace NexusLabs.Needlr.Copilot.Tests;

/// <summary>
/// Integration tests that call the real Copilot API.
/// Requires a logged-in Copilot CLI or GH_TOKEN environment variable.
/// Run manually: dotnet test --filter "Category=Integration"
/// </summary>
[Trait("Category", "Integration")]
public class IntegrationSmokeTests
{
    [Fact]
    public async Task GetResponseAsync_ReturnsTextResponse()
    {
        using var client = CreateClient();

        var response = await client.GetResponseAsync(
        [
            new(ChatRole.System, "You are a helpful assistant. Respond concisely in one sentence."),
            new(ChatRole.User, "What is the capital of France?"),
        ],
        cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(response);
        Assert.NotNull(response.ModelId);

        var text = string.Join("", response.Messages
            .SelectMany(m => m.Contents.OfType<TextContent>())
            .Select(t => t.Text));

        Assert.NotEmpty(text);
        Assert.Contains("Paris", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetStreamingResponseAsync_YieldsUpdates()
    {
        using var client = CreateClient();

        var updates = new List<ChatResponseUpdate>();
        await foreach (var update in client.GetStreamingResponseAsync(
        [
            new(ChatRole.User, "Say exactly: hello world"),
        ],
        cancellationToken: TestContext.Current.CancellationToken))
        {
            updates.Add(update);
        }

        Assert.NotEmpty(updates);

        var text = string.Join("", updates
            .SelectMany(u => u.Contents.OfType<TextContent>())
            .Select(t => t.Text));

        Assert.NotEmpty(text);
    }

    [Fact]
    public async Task GetResponseAsync_NoExtraToolsLeaked()
    {
        using var client = CreateClient();

        var response = await client.GetResponseAsync(
        [
            new(ChatRole.User, "List all tools/functions you can call. If none, say 'NO TOOLS'."),
        ],
        cancellationToken: TestContext.Current.CancellationToken);

        var text = string.Join("", response.Messages
            .SelectMany(m => m.Contents.OfType<TextContent>())
            .Select(t => t.Text));

        Assert.DoesNotContain("grep", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("powershell", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("edit_file", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WebSearchFunction_ReturnsResults()
    {
        var options = new CopilotChatClientOptions();
        var oauthProvider = new GitHubOAuthTokenProvider(options);
        using var mcpClient = new CopilotMcpToolClient(oauthProvider, options);
        var webSearch = new CopilotWebSearchFunction(mcpClient);

        var args = new AIFunctionArguments(
            new Dictionary<string, object?> { ["query"] = "What is the capital of France?" });

        var result = await webSearch.InvokeAsync(args, TestContext.Current.CancellationToken);
        var text = result?.ToString() ?? "";

        Assert.NotEmpty(text);
        Assert.Contains("Paris", text, StringComparison.OrdinalIgnoreCase);
    }

    private static CopilotChatClient CreateClient()
    {
        return new CopilotChatClient(new CopilotChatClientOptions
        {
            DefaultModel = "claude-sonnet-4",
        });
    }
}
