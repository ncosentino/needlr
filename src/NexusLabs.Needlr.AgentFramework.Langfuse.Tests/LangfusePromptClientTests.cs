using System.Net;
using System.Text.Json;

namespace NexusLabs.Needlr.AgentFramework.Langfuse.Tests;

public sealed class LangfusePromptClientTests
{
    private static readonly Uri BaseUrl = new("https://lf.example/");

    [Fact]
    public async Task GetPromptAsync_ByLabel_BuildsUrlAndParsesPrompt()
    {
        var captured = new List<CapturedRequest>();
        using var http = LangfuseHttpStub.Create(
            _ => LangfuseHttpStub.Json(HttpStatusCode.OK, "{\"name\":\"trip-planner\",\"version\":2,\"type\":\"text\",\"prompt\":\"You plan trips.\",\"labels\":[\"production\"],\"tags\":[]}"),
            captured);
        var client = new LangfusePromptClient(new LangfuseApiClient(http, BaseUrl, "Basic x"));

        var prompt = await client.GetPromptAsync("trip-planner", label: "production", cancellationToken: TestContext.Current.CancellationToken);

        var get = Assert.Single(captured);
        Assert.Equal(HttpMethod.Get, get.Method);
        Assert.Equal("/api/public/v2/prompts/trip-planner", get.Uri.AbsolutePath);
        Assert.Contains("label=production", get.Uri.Query, StringComparison.Ordinal);

        Assert.NotNull(prompt);
        Assert.Equal("trip-planner", prompt!.Name);
        Assert.Equal(2, prompt.Version);
        Assert.Equal("text", prompt.Type);
        Assert.Equal("You plan trips.", prompt.Text);
        Assert.Contains("production", prompt.Labels);
    }

    [Fact]
    public async Task GetPromptAsync_ByVersion_PrefersVersionOverLabel()
    {
        var captured = new List<CapturedRequest>();
        using var http = LangfuseHttpStub.Create(
            _ => LangfuseHttpStub.Json(HttpStatusCode.OK, "{\"name\":\"p\",\"version\":5,\"type\":\"text\",\"prompt\":\"x\"}"),
            captured);
        var client = new LangfusePromptClient(new LangfuseApiClient(http, BaseUrl, "Basic x"));

        await client.GetPromptAsync("p", label: "production", version: 5, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains("version=5", Assert.Single(captured).Uri.Query, StringComparison.Ordinal);
        Assert.DoesNotContain("label=", Assert.Single(captured).Uri.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetPromptAsync_WhenMissing_ReturnsNull()
    {
        var captured = new List<CapturedRequest>();
        using var http = LangfuseHttpStub.Create(_ => new HttpResponseMessage(HttpStatusCode.NotFound), captured);
        var client = new LangfusePromptClient(new LangfuseApiClient(http, BaseUrl, "Basic x"));

        var prompt = await client.GetPromptAsync("missing", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(prompt);
    }

    [Fact]
    public async Task CreateTextPromptAsync_PostsBodyAndParsesResponse()
    {
        var captured = new List<CapturedRequest>();
        using var http = LangfuseHttpStub.Create(
            _ => LangfuseHttpStub.Json(HttpStatusCode.Created, "{\"name\":\"p\",\"version\":1,\"type\":\"text\",\"prompt\":\"content\",\"labels\":[\"production\"]}"),
            captured);
        var client = new LangfusePromptClient(new LangfuseApiClient(http, BaseUrl, "Basic x"));

        var prompt = await client.CreateTextPromptAsync("p", "content", ["production"], TestContext.Current.CancellationToken);

        var post = Assert.Single(captured);
        Assert.Equal(HttpMethod.Post, post.Method);
        Assert.EndsWith("/api/public/v2/prompts", post.Uri.AbsolutePath, StringComparison.Ordinal);
        using var json = JsonDocument.Parse(post.Body!);
        Assert.Equal("p", json.RootElement.GetProperty("name").GetString());
        Assert.Equal("text", json.RootElement.GetProperty("type").GetString());
        Assert.Equal("content", json.RootElement.GetProperty("prompt").GetString());
        Assert.Equal("production", json.RootElement.GetProperty("labels")[0].GetString());

        Assert.Equal(1, prompt.Version);
        Assert.Equal("content", prompt.Text);
    }
}
