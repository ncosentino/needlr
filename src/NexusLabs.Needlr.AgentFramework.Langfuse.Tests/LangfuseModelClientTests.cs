using System.Net;
using System.Text.Json;

namespace NexusLabs.Needlr.AgentFramework.Langfuse.Tests;

public sealed class LangfuseModelClientTests
{
    private static readonly Uri BaseUrl = new("https://lf.example/");

    [Fact]
    public async Task EnsureModelPriceAsync_WhenAbsent_CreatesModel()
    {
        var captured = new List<CapturedRequest>();
        using var httpClient = LangfuseHttpStub.Create(
            request => request.Method == HttpMethod.Get
                ? LangfuseHttpStub.Json(HttpStatusCode.OK, "{\"data\":[],\"meta\":{\"totalPages\":1}}")
                : new HttpResponseMessage(HttpStatusCode.OK),
            captured);

        var client = CreateClient(httpClient);

        await client.EnsureModelPriceAsync(
            new LangfuseModelPrice
            {
                ModelName = "needlr-mock",
                MatchPattern = "(?i)^needlr-mock$",
                InputPrice = 0.000001,
                OutputPrice = 0.000002,
            },
            TestContext.Current.CancellationToken);

        var post = Assert.Single(captured, c => c.Method == HttpMethod.Post);
        Assert.EndsWith("/api/public/models", post.Uri.AbsolutePath, StringComparison.Ordinal);

        using var json = JsonDocument.Parse(post.Body!);
        Assert.Equal("needlr-mock", json.RootElement.GetProperty("modelName").GetString());
        Assert.Equal("(?i)^needlr-mock$", json.RootElement.GetProperty("matchPattern").GetString());
        Assert.Equal(0.000001, json.RootElement.GetProperty("inputPrice").GetDouble());
        Assert.Equal(0.000002, json.RootElement.GetProperty("outputPrice").GetDouble());
        Assert.Equal("TOKENS", json.RootElement.GetProperty("unit").GetString());
    }

    [Fact]
    public async Task EnsureModelPriceAsync_WhenPresent_DoesNotCreate()
    {
        var captured = new List<CapturedRequest>();
        using var httpClient = LangfuseHttpStub.Create(
            request => request.Method == HttpMethod.Get
                ? LangfuseHttpStub.Json(
                    HttpStatusCode.OK,
                    "{\"data\":[{\"modelName\":\"needlr-mock\",\"matchPattern\":\"(?i)^needlr-mock$\",\"unit\":\"TOKENS\",\"totalPrice\":0.00001}],\"meta\":{\"totalPages\":1}}")
                : new HttpResponseMessage(HttpStatusCode.OK),
            captured);

        var client = CreateClient(httpClient);

        await client.EnsureModelPriceAsync(
            new LangfuseModelPrice { ModelName = "needlr-mock", MatchPattern = "(?i)^needlr-mock$", TotalPrice = 0.00001 },
            TestContext.Current.CancellationToken);

        Assert.DoesNotContain(captured, c => c.Method == HttpMethod.Post);
    }

    private static LangfuseModelClient CreateClient(HttpClient httpClient) =>
        new(
            new LangfuseApiClient(httpClient, BaseUrl, "Basic x"),
            new LangfuseInProcessResourceLockProvider(),
            "project");
}
