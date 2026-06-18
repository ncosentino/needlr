using System.Net;
using System.Text.Json;

namespace NexusLabs.Needlr.AgentFramework.Langfuse.Tests;

public sealed class LangfuseDatasetClientTests
{
    private static readonly Uri BaseUrl = new("https://lf.example/");

    [Fact]
    public async Task EnsureDatasetAsync_WhenMissing_CreatesDataset()
    {
        var captured = new List<CapturedRequest>();
        using var httpClient = LangfuseHttpStub.Create(
            request => request.Method == HttpMethod.Get
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : LangfuseHttpStub.Json(HttpStatusCode.OK, "{\"name\":\"evals\"}"),
            captured);

        var client = new LangfuseDatasetClient(new LangfuseApiClient(httpClient, BaseUrl, "Basic x"));

        await client.EnsureDatasetAsync("evals", "regression suite", TestContext.Current.CancellationToken);

        var get = Assert.Single(captured, c => c.Method == HttpMethod.Get);
        Assert.EndsWith("/api/public/v2/datasets/evals", get.Uri.AbsolutePath, StringComparison.Ordinal);

        var post = Assert.Single(captured, c => c.Method == HttpMethod.Post);
        Assert.EndsWith("/api/public/v2/datasets", post.Uri.AbsolutePath, StringComparison.Ordinal);
        using var json = JsonDocument.Parse(post.Body!);
        Assert.Equal("evals", json.RootElement.GetProperty("name").GetString());
        Assert.Equal("regression suite", json.RootElement.GetProperty("description").GetString());
    }

    [Fact]
    public async Task EnsureDatasetAsync_WhenPresent_DoesNotCreate()
    {
        var captured = new List<CapturedRequest>();
        using var httpClient = LangfuseHttpStub.Create(
            _ => LangfuseHttpStub.Json(HttpStatusCode.OK, "{\"name\":\"evals\"}"),
            captured);

        var client = new LangfuseDatasetClient(new LangfuseApiClient(httpClient, BaseUrl, "Basic x"));

        await client.EnsureDatasetAsync("evals", cancellationToken: TestContext.Current.CancellationToken);

        Assert.DoesNotContain(captured, c => c.Method == HttpMethod.Post);
    }

    [Fact]
    public async Task UpsertItemAsync_PostsItemWithSerializedPayload()
    {
        var captured = new List<CapturedRequest>();
        using var httpClient = LangfuseHttpStub.Create(
            _ => new HttpResponseMessage(HttpStatusCode.OK),
            captured);

        var client = new LangfuseDatasetClient(new LangfuseApiClient(httpClient, BaseUrl, "Basic x"));

        await client.UpsertItemAsync(
            new LangfuseDatasetItem
            {
                DatasetName = "evals",
                Id = "case-1",
                Input = new { question = "2+2?" },
                ExpectedOutput = "4",
            },
            TestContext.Current.CancellationToken);

        var post = Assert.Single(captured);
        Assert.Equal(HttpMethod.Post, post.Method);
        Assert.EndsWith("/api/public/dataset-items", post.Uri.AbsolutePath, StringComparison.Ordinal);

        using var json = JsonDocument.Parse(post.Body!);
        Assert.Equal("evals", json.RootElement.GetProperty("datasetName").GetString());
        Assert.Equal("case-1", json.RootElement.GetProperty("id").GetString());
        Assert.Equal("2+2?", json.RootElement.GetProperty("input").GetProperty("question").GetString());
        Assert.Equal("4", json.RootElement.GetProperty("expectedOutput").GetString());
    }
}
