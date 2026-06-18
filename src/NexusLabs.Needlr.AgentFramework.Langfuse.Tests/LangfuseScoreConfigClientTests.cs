using System.Net;
using System.Text.Json;

namespace NexusLabs.Needlr.AgentFramework.Langfuse.Tests;

public sealed class LangfuseScoreConfigClientTests
{
    private static readonly Uri BaseUrl = new("https://lf.example/");

    [Fact]
    public async Task EnsureScoreConfigAsync_WhenAbsent_CreatesConfig()
    {
        var captured = new List<CapturedRequest>();
        using var httpClient = LangfuseHttpStub.Create(
            request => request.Method == HttpMethod.Get
                ? LangfuseHttpStub.Json(HttpStatusCode.OK, "{\"data\":[],\"meta\":{\"totalPages\":1}}")
                : new HttpResponseMessage(HttpStatusCode.OK),
            captured);

        var client = new LangfuseScoreConfigClient(new LangfuseApiClient(httpClient, BaseUrl, "Basic x"));

        await client.EnsureScoreConfigAsync(
            new LangfuseScoreConfig
            {
                Name = "correctness",
                DataType = LangfuseScoreDataType.Numeric,
                MinValue = 0,
                MaxValue = 1,
                Description = "0..1 correctness",
            },
            TestContext.Current.CancellationToken);

        var post = Assert.Single(captured, c => c.Method == HttpMethod.Post);
        Assert.EndsWith("/api/public/score-configs", post.Uri.AbsolutePath, StringComparison.Ordinal);

        using var json = JsonDocument.Parse(post.Body!);
        Assert.Equal("correctness", json.RootElement.GetProperty("name").GetString());
        Assert.Equal("NUMERIC", json.RootElement.GetProperty("dataType").GetString());
        Assert.Equal(0, json.RootElement.GetProperty("minValue").GetDouble());
        Assert.Equal(1, json.RootElement.GetProperty("maxValue").GetDouble());
    }

    [Fact]
    public async Task EnsureScoreConfigAsync_WhenPresent_DoesNotCreate()
    {
        var captured = new List<CapturedRequest>();
        using var httpClient = LangfuseHttpStub.Create(
            request => request.Method == HttpMethod.Get
                ? LangfuseHttpStub.Json(HttpStatusCode.OK, "{\"data\":[{\"name\":\"correctness\"}],\"meta\":{\"totalPages\":1}}")
                : new HttpResponseMessage(HttpStatusCode.OK),
            captured);

        var client = new LangfuseScoreConfigClient(new LangfuseApiClient(httpClient, BaseUrl, "Basic x"));

        await client.EnsureScoreConfigAsync(
            new LangfuseScoreConfig { Name = "correctness", DataType = LangfuseScoreDataType.Numeric },
            TestContext.Current.CancellationToken);

        Assert.DoesNotContain(captured, c => c.Method == HttpMethod.Post);
    }

    [Fact]
    public async Task EnsureScoreConfigAsync_Categorical_SendsCategoriesNotRange()
    {
        var captured = new List<CapturedRequest>();
        using var httpClient = LangfuseHttpStub.Create(
            request => request.Method == HttpMethod.Get
                ? LangfuseHttpStub.Json(HttpStatusCode.OK, "{\"data\":[],\"meta\":{\"totalPages\":1}}")
                : new HttpResponseMessage(HttpStatusCode.OK),
            captured);

        var client = new LangfuseScoreConfigClient(new LangfuseApiClient(httpClient, BaseUrl, "Basic x"));

        await client.EnsureScoreConfigAsync(
            new LangfuseScoreConfig
            {
                Name = "verdict",
                DataType = LangfuseScoreDataType.Categorical,
                MinValue = 0,
                MaxValue = 1,
                Categories =
                [
                    new LangfuseScoreConfigCategory("pass", 1),
                    new LangfuseScoreConfigCategory("fail", 0),
                ],
            },
            TestContext.Current.CancellationToken);

        var post = Assert.Single(captured, c => c.Method == HttpMethod.Post);
        using var json = JsonDocument.Parse(post.Body!);
        Assert.Equal("CATEGORICAL", json.RootElement.GetProperty("dataType").GetString());
        Assert.Equal(2, json.RootElement.GetProperty("categories").GetArrayLength());
        Assert.False(json.RootElement.TryGetProperty("minValue", out _));
        Assert.False(json.RootElement.TryGetProperty("maxValue", out _));
    }
}
