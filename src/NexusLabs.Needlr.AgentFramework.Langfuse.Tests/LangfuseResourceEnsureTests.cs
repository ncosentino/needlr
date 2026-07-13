using System.Net;

namespace NexusLabs.Needlr.AgentFramework.Langfuse.Tests;

public sealed class LangfuseResourceEnsureTests
{
    private static readonly Uri BaseUrl = new("https://lf.example/");
    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;

    [Fact]
    public async Task EnsureScoreConfigAsync_TwoClientCompositionsSharingLock_CreateOnce()
    {
        var created = 0;
        var posts = 0;
        var handler = new TrackingHttpMessageHandler(request =>
        {
            if (request.Method == HttpMethod.Get)
            {
                return Volatile.Read(ref created) == 0
                    ? LangfuseHttpStub.Json(
                        HttpStatusCode.OK,
                        """{"data":[],"meta":{"totalPages":1}}""")
                    : LangfuseHttpStub.Json(
                        HttpStatusCode.OK,
                        """{"data":[{"name":"quality","dataType":"NUMERIC","minValue":0,"maxValue":1,"description":"quality range"}],"meta":{"totalPages":1}}""");
            }

            Interlocked.Increment(ref posts);
            Volatile.Write(ref created, 1);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var httpClient = new HttpClient(handler);
        var locks = new LangfuseInProcessResourceLockProvider();
        var first = new LangfuseScoreConfigClient(CreateApiClient(httpClient), locks, "project");
        var second = new LangfuseScoreConfigClient(CreateApiClient(httpClient), locks, "project");
        var config = new LangfuseScoreConfig
        {
            Name = "quality",
            DataType = LangfuseScoreDataType.Numeric,
            MinValue = 0,
            MaxValue = 1,
            Description = "quality range",
        };

        await Task.WhenAll(
            first.EnsureScoreConfigAsync(config, _cancellationToken),
            second.EnsureScoreConfigAsync(config, _cancellationToken));

        Assert.Equal(1, posts);
    }

    [Fact]
    public async Task EnsureModelPriceAsync_AmbiguousServerFailure_ReconcilesCreatedModel()
    {
        var created = 0;
        var gets = 0;
        var posts = 0;
        var handler = new TrackingHttpMessageHandler(request =>
        {
            if (request.Method == HttpMethod.Get)
            {
                Interlocked.Increment(ref gets);
                return Volatile.Read(ref created) == 0
                    ? LangfuseHttpStub.Json(
                        HttpStatusCode.OK,
                        """{"data":[],"meta":{"totalPages":1}}""")
                    : LangfuseHttpStub.Json(
                        HttpStatusCode.OK,
                        """{"data":[{"modelName":"needlr-mock","matchPattern":"(?i)^needlr-mock$","unit":"TOKENS","inputPrice":0.000001,"outputPrice":0.000002}],"meta":{"totalPages":1}}""");
            }

            Interlocked.Increment(ref posts);
            Volatile.Write(ref created, 1);
            return new HttpResponseMessage(HttpStatusCode.InternalServerError);
        });
        using var httpClient = new HttpClient(handler);
        var client = new LangfuseModelClient(
            CreateApiClient(httpClient),
            new LangfuseInProcessResourceLockProvider(),
            "project");

        await client.EnsureModelPriceAsync(
            new LangfuseModelPrice
            {
                ModelName = "needlr-mock",
                MatchPattern = "(?i)^needlr-mock$",
                Unit = "TOKENS",
                InputPrice = 0.000001,
                OutputPrice = 0.000002,
            },
            _cancellationToken);

        Assert.Equal(1, posts);
        Assert.Equal(2, gets);
    }

    [Fact]
    public async Task EnsureScoreConfigAsync_ExistingDifferentSchema_FailsWithoutCreating()
    {
        var posts = 0;
        var handler = new TrackingHttpMessageHandler(request =>
        {
            if (request.Method == HttpMethod.Post)
            {
                Interlocked.Increment(ref posts);
            }

            return LangfuseHttpStub.Json(
                HttpStatusCode.OK,
                """{"data":[{"name":"quality","dataType":"BOOLEAN"}],"meta":{"totalPages":1}}""");
        });
        using var httpClient = new HttpClient(handler);
        var client = new LangfuseScoreConfigClient(
            CreateApiClient(httpClient),
            new LangfuseInProcessResourceLockProvider(),
            "project");

        await Assert.ThrowsAnyAsync<LangfuseException>(() => client.EnsureScoreConfigAsync(
            new LangfuseScoreConfig
            {
                Name = "quality",
                DataType = LangfuseScoreDataType.Numeric,
                MinValue = 0,
                MaxValue = 1,
            },
            _cancellationToken));

        Assert.Equal(0, posts);
    }

    [Fact]
    public async Task EnsureDatasetAsync_ParallelIdenticalUpserts_AreRetrySafe()
    {
        var posts = 0;
        var handler = new TrackingHttpMessageHandler(request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Interlocked.Increment(ref posts);
            return LangfuseHttpStub.Json(
                HttpStatusCode.OK,
                """{"name":"evals","description":"regression"}""");
        });
        using var httpClient = new HttpClient(handler);
        var client = new LangfuseDatasetClient(CreateApiClient(httpClient));

        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
            client.EnsureDatasetAsync(
                "evals",
                "regression",
                _cancellationToken)));

        Assert.Equal(8, posts);
    }

    private static LangfuseApiClient CreateApiClient(HttpClient httpClient) =>
        new(
            httpClient,
            BaseUrl,
            "Basic x",
            new LangfuseHttpOptions
            {
                RequestTimeout = TimeSpan.FromSeconds(10),
                MaxAttempts = 2,
                InitialRetryDelay = TimeSpan.Zero,
                MaxRetryDelay = TimeSpan.Zero,
            });
}
