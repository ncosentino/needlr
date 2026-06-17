using System.Net;
using System.Text.Json;

using Moq;
using Moq.Protected;

namespace NexusLabs.Needlr.AgentFramework.Langfuse.Tests;

public sealed class LangfuseScoreApiClientTests
{
    [Fact]
    public async Task CreateAsync_PostsScoreWithBasicAuthAndExpectedBody()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;

        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns(async (HttpRequestMessage request, CancellationToken token) =>
            {
                capturedRequest = request;
                capturedBody = request.Content is null
                    ? null
                    : await request.Content.ReadAsStringAsync(token);
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

        using var httpClient = new HttpClient(handler.Object, disposeHandler: false);
        var client = new LangfuseScoreApiClient(
            httpClient,
            new Uri("https://cloud.langfuse.com/api/public/scores"),
            "Basic cGs6c2s=");

        var score = new LangfuseScore
        {
            TraceId = "abc123",
            Name = "tool_calls_all_succeeded",
            Value = 1.0,
            DataType = "BOOLEAN",
            Comment = "all tools ok",
        };

        await client.CreateAsync(score, CancellationToken.None);

        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest!.Method);
        Assert.Equal("https://cloud.langfuse.com/api/public/scores", capturedRequest.RequestUri!.ToString());
        Assert.Equal("Basic", capturedRequest.Headers.Authorization!.Scheme);
        Assert.Equal("cGs6c2s=", capturedRequest.Headers.Authorization.Parameter);

        using var json = JsonDocument.Parse(capturedBody!);
        var root = json.RootElement;
        Assert.Equal("abc123", root.GetProperty("traceId").GetString());
        Assert.Equal("tool_calls_all_succeeded", root.GetProperty("name").GetString());
        Assert.Equal(1.0, root.GetProperty("value").GetDouble());
        Assert.Equal("BOOLEAN", root.GetProperty("dataType").GetString());
        Assert.Equal("all tools ok", root.GetProperty("comment").GetString());
    }

    [Fact]
    public async Task CreateAsync_CategoricalScore_SerializesStringValue()
    {
        string? capturedBody = null;

        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns(async (HttpRequestMessage request, CancellationToken token) =>
            {
                capturedBody = await request.Content!.ReadAsStringAsync(token);
                return new HttpResponseMessage(HttpStatusCode.Created);
            });

        using var httpClient = new HttpClient(handler.Object, disposeHandler: false);
        var client = new LangfuseScoreApiClient(
            httpClient,
            new Uri("https://cloud.langfuse.com/api/public/scores"),
            "Basic cGs6c2s=");

        await client.CreateAsync(
            new LangfuseScore { TraceId = "t", Name = "verdict", Value = "partially correct", DataType = "CATEGORICAL" },
            CancellationToken.None);

        using var json = JsonDocument.Parse(capturedBody!);
        Assert.Equal("partially correct", json.RootElement.GetProperty("value").GetString());
        Assert.False(json.RootElement.TryGetProperty("comment", out _));
    }

    [Fact]
    public async Task CreateAsync_NonSuccessStatus_ThrowsLangfuseException()
    {
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("invalid credentials"),
            });

        using var httpClient = new HttpClient(handler.Object, disposeHandler: false);
        var client = new LangfuseScoreApiClient(
            httpClient,
            new Uri("https://cloud.langfuse.com/api/public/scores"),
            "Basic cGs6c2s=");

        var ex = await Assert.ThrowsAsync<LangfuseException>(() => client.CreateAsync(
            new LangfuseScore { TraceId = "t", Name = "n", Value = 1.0, DataType = "NUMERIC" },
            CancellationToken.None));

        Assert.Contains("401", ex.Message, StringComparison.Ordinal);
        Assert.Contains("invalid credentials", ex.Message, StringComparison.Ordinal);
    }
}
