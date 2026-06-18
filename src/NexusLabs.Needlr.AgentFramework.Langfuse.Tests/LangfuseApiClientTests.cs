using System.Net;
using System.Text.Json;

using Moq;
using Moq.Protected;

namespace NexusLabs.Needlr.AgentFramework.Langfuse.Tests;

public sealed class LangfuseApiClientTests
{
    private sealed record SamplePayload(string Name, int Count);

    private sealed record SampleResponse(string Id);

    [Fact]
    public async Task PostAsync_ComposesPathWithBasicAuthAndCamelCaseBody()
    {
        HttpRequestMessage? captured = null;
        string? capturedBody = null;

        using var httpClient = CreateClient(
            (request, body) => { captured = request; capturedBody = body; },
            () => new HttpResponseMessage(HttpStatusCode.OK));

        var client = new LangfuseApiClient(
            httpClient,
            new Uri("https://cloud.langfuse.com/"),
            "Basic cGs6c2s=");

        await client.PostAsync(
            "api/public/dataset-run-items",
            new SamplePayload("run-1", 3),
            TestContext.Current.CancellationToken);

        Assert.NotNull(captured);
        Assert.Equal(HttpMethod.Post, captured!.Method);
        Assert.Equal("https://cloud.langfuse.com/api/public/dataset-run-items", captured.RequestUri!.ToString());
        Assert.Equal("Basic", captured.Headers.Authorization!.Scheme);
        Assert.Equal("cGs6c2s=", captured.Headers.Authorization.Parameter);

        using var json = JsonDocument.Parse(capturedBody!);
        Assert.Equal("run-1", json.RootElement.GetProperty("name").GetString());
        Assert.Equal(3, json.RootElement.GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task PostAsync_WithResponse_DeserializesBody()
    {
        using var httpClient = CreateClient(
            (_, _) => { },
            () => new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent("{\"id\":\"dataset-42\"}"),
            });

        var client = new LangfuseApiClient(httpClient, new Uri("https://lf.example/"), "Basic x");

        var response = await client.PostAsync<SamplePayload, SampleResponse>(
            "api/public/v2/datasets",
            new SamplePayload("n", 1),
            TestContext.Current.CancellationToken);

        Assert.NotNull(response);
        Assert.Equal("dataset-42", response!.Id);
    }

    [Fact]
    public async Task GetAsync_DeserializesBody()
    {
        HttpRequestMessage? captured = null;

        using var httpClient = CreateClient(
            (request, _) => captured = request,
            () => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"id\":\"cfg-1\"}"),
            });

        var client = new LangfuseApiClient(httpClient, new Uri("https://lf.example/"), "Basic x");

        var response = await client.GetAsync<SampleResponse>(
            "api/public/score-configs",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Get, captured!.Method);
        Assert.Equal("https://lf.example/api/public/score-configs", captured.RequestUri!.ToString());
        Assert.Equal("cfg-1", response!.Id);
    }

    [Fact]
    public async Task PostAsync_NonSuccess_ThrowsLangfuseExceptionWithStatusAndBody()
    {
        using var httpClient = CreateClient(
            (_, _) => { },
            () => new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("invalid datasetItemId"),
            });

        var client = new LangfuseApiClient(httpClient, new Uri("https://lf.example/"), "Basic x");

        var ex = await Assert.ThrowsAsync<LangfuseException>(() => client.PostAsync(
            "api/public/dataset-run-items",
            new SamplePayload("n", 1),
            TestContext.Current.CancellationToken));

        Assert.Contains("400", ex.Message, StringComparison.Ordinal);
        Assert.Contains("invalid datasetItemId", ex.Message, StringComparison.Ordinal);
    }

    private static HttpClient CreateClient(
        Action<HttpRequestMessage, string?> onRequest,
        Func<HttpResponseMessage> respond)
    {
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns(async (HttpRequestMessage request, CancellationToken token) =>
            {
                var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(token);
                onRequest(request, body);
                return respond();
            });

        return new HttpClient(handler.Object, disposeHandler: false);
    }
}
