using System.Net;

using Microsoft.Extensions.Time.Testing;

namespace NexusLabs.Needlr.AgentFramework.Langfuse.Tests;

public sealed class LangfuseApiClientResilienceTests
{
    private static readonly Uri BaseUrl = new("https://lf.example/");
    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;

    [Fact]
    public async Task CreateScoreAsync_WithStableId_RetriesTransientFailureWithIdenticalBody()
    {
        var attempts = 0;
        var handler = new TrackingHttpMessageHandler(_ =>
        {
            var attempt = Interlocked.Increment(ref attempts);
            return attempt == 1
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                : LangfuseHttpStub.Json(HttpStatusCode.OK, """{"id":"score-1"}""");
        });
        using var httpClient = new HttpClient(handler);
        var timeProvider = new FakeTimeProvider();
        var health = new LangfusePublicationHealth(isEnabled: true);
        var apiClient = CreateApiClient(httpClient, timeProvider, health);
        var scoreClient = new LangfuseScoreApiClient(apiClient);
        var score = new LangfuseScore
        {
            Id = "score-1",
            TraceId = "trace-1",
            Name = "quality",
            Value = 0.9,
            DataType = "NUMERIC",
        };

        var createTask = scoreClient.CreateAsync(score, _cancellationToken);
        await WaitForAttemptsAsync(() => Volatile.Read(ref attempts), 1);
        await AdvanceUntilAttemptsAsync(timeProvider, () => Volatile.Read(ref attempts), 2);

        var scoreId = await createTask;

        Assert.Equal("score-1", scoreId);
        Assert.Equal(2, handler.CapturedRequests.Count);
        Assert.Equal(
            handler.CapturedRequests[0].Body,
            handler.CapturedRequests[1].Body);
        var retries = health.GetSnapshot().Retries;
        Assert.Equal(1, retries.Total);
        Assert.Equal(1, retries.TransientServer);
    }

    [Fact]
    public async Task CreateScoreAsync_WithoutStableId_DoesNotRetryTransientFailure()
    {
        var attempts = 0;
        var handler = new TrackingHttpMessageHandler(_ =>
        {
            Interlocked.Increment(ref attempts);
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        });
        using var httpClient = new HttpClient(handler);
        var apiClient = CreateApiClient(httpClient, new FakeTimeProvider());
        var scoreClient = new LangfuseScoreApiClient(apiClient);

        await Assert.ThrowsAnyAsync<LangfuseException>(() => scoreClient.CreateAsync(
            new LangfuseScore
            {
                TraceId = "trace-1",
                Name = "quality",
                Value = 0.9,
                DataType = "NUMERIC",
            },
            _cancellationToken));

        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task GetAsync_WhenRateLimited_UsesRetryAfterAndBoundedAttempts()
    {
        var attempts = 0;
        var handler = new TrackingHttpMessageHandler(_ =>
        {
            var attempt = Interlocked.Increment(ref attempts);
            if (attempt == 1)
            {
                var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(
                    TimeSpan.FromSeconds(3));
                return response;
            }

            return LangfuseHttpStub.Json(HttpStatusCode.OK, """{"name":"evals"}""");
        });
        using var httpClient = new HttpClient(handler);
        var timeProvider = new FakeTimeProvider();
        var health = new LangfusePublicationHealth(isEnabled: true);
        var apiClient = CreateApiClient(httpClient, timeProvider, health);

        var getTask = apiClient.GetAsync<LangfuseDatasetRef>(
            "api/public/v2/datasets/evals",
            _cancellationToken);
        await WaitForAttemptsAsync(() => Volatile.Read(ref attempts), 1);
        await AdvanceUntilAttemptsAsync(timeProvider, () => Volatile.Read(ref attempts), 2);

        var dataset = await getTask;

        Assert.NotNull(dataset);
        Assert.Equal("evals", dataset!.Name);
        Assert.Equal(2, attempts);
        Assert.Equal(1, health.GetSnapshot().Retries.RateLimited);
    }

    [Fact]
    public async Task CreateScoreAsync_RequestTimeout_RetriesOnlyBecauseScoreHasStableId()
    {
        var attempts = 0;
        var pendingResponse = new TaskCompletionSource<HttpResponseMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var httpClient = new HttpClient(new DelegateHttpMessageHandler(
            (_, token) =>
            {
                var attempt = Interlocked.Increment(ref attempts);
                return attempt == 1
                    ? pendingResponse.Task.WaitAsync(token)
                    : Task.FromResult(
                        LangfuseHttpStub.Json(HttpStatusCode.OK, """{"id":"score-1"}"""));
            }));
        var timeProvider = new FakeTimeProvider();
        var health = new LangfusePublicationHealth(isEnabled: true);
        var apiClient = new LangfuseApiClient(
            httpClient,
            BaseUrl,
            "Basic x",
            new LangfuseHttpOptions
            {
                RequestTimeout = TimeSpan.FromSeconds(2),
                MaxAttempts = 2,
                InitialRetryDelay = TimeSpan.Zero,
                MaxRetryDelay = TimeSpan.Zero,
            },
            timeProvider,
            health);
        var scoreClient = new LangfuseScoreApiClient(apiClient);

        var createTask = scoreClient.CreateAsync(
            new LangfuseScore
            {
                Id = "score-1",
                TraceId = "trace-1",
                Name = "quality",
                Value = 0.9,
                DataType = "NUMERIC",
            },
            _cancellationToken);
        await WaitForAttemptsAsync(() => Volatile.Read(ref attempts), 1);
        await AdvanceUntilAttemptsAsync(timeProvider, () => Volatile.Read(ref attempts), 2);

        Assert.Equal("score-1", await createTask);
        Assert.Equal(1, health.GetSnapshot().Retries.TimedOut);
    }

    [Fact]
    public async Task CreateScoreAsync_WhenCallerCancelsRetryDelay_PreservesCallerToken()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken);
        var attempts = 0;
        var handler = new TrackingHttpMessageHandler(_ =>
        {
            Interlocked.Increment(ref attempts);
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(
                TimeSpan.FromSeconds(3));
            return response;
        });
        using var httpClient = new HttpClient(handler);
        var apiClient = CreateApiClient(httpClient, new FakeTimeProvider());
        var scoreClient = new LangfuseScoreApiClient(apiClient);

        var createTask = scoreClient.CreateAsync(
            new LangfuseScore
            {
                Id = "score-1",
                TraceId = "trace-1",
                Name = "quality",
                Value = 0.9,
                DataType = "NUMERIC",
            },
            cancellation.Token);
        await WaitForAttemptsAsync(() => Volatile.Read(ref attempts), 1);
        cancellation.Cancel();

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => createTask);

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.Equal(1, attempts);
    }

    private static LangfuseApiClient CreateApiClient(
        HttpClient httpClient,
        TimeProvider timeProvider,
        LangfusePublicationHealth? health = null) =>
        new(
            httpClient,
            BaseUrl,
            "Basic x",
            new LangfuseHttpOptions
            {
                RequestTimeout = TimeSpan.FromSeconds(10),
                MaxAttempts = 2,
                InitialRetryDelay = TimeSpan.FromSeconds(1),
                MaxRetryDelay = TimeSpan.FromSeconds(3),
            },
            timeProvider,
            health);

    private async Task WaitForAttemptsAsync(Func<int> getAttempts, int expected)
    {
        while (getAttempts() < expected)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
        }
    }

    private async Task AdvanceUntilAttemptsAsync(
        FakeTimeProvider timeProvider,
        Func<int> getAttempts,
        int expected)
    {
        for (var i = 0; i < 10 && getAttempts() < expected; i++)
        {
            timeProvider.Advance(TimeSpan.FromSeconds(1));
            await Task.Yield();
        }

        Assert.Equal(expected, getAttempts());
    }
}
