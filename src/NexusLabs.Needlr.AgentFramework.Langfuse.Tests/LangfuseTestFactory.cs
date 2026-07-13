using System.Diagnostics;
using System.Net;

namespace NexusLabs.Needlr.AgentFramework.Langfuse.Tests;

/// <summary>Shared construction helpers for tests that exercise scenarios and experiment runs.</summary>
internal static class LangfuseTestFactory
{
    public static ActivityListener StartListener(
        Action<Activity>? onStarted = null,
        Action<Activity>? onStopped = null)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == LangfuseActivitySource.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = onStarted,
            ActivityStopped = onStopped,
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    public static LangfuseScoreRecorder OkScoreRecorder(List<CapturedRequest>? captured = null)
    {
        var list = captured ?? [];
        var httpClient = LangfuseHttpStub.Create(LangfuseHttpStub.ScoreAccepted, list);
        var apiClient = CreateScoreApiClient(httpClient);
        return new LangfuseScoreRecorder(
            apiClient,
            new LangfuseScoreFailureSink(LangfuseScoreFailureMode.Strict, null),
            normalizeNames: false);
    }

    public static LangfuseScoreApiClient CreateScoreApiClient(
        HttpClient httpClient,
        Uri? baseUrl = null,
        string authorizationHeaderValue = "Basic x",
        LangfuseHttpOptions? httpOptions = null,
        TimeProvider? timeProvider = null) =>
        new(new LangfuseApiClient(
            httpClient,
            baseUrl ?? new Uri("https://lf.example/"),
            authorizationHeaderValue,
            httpOptions,
            timeProvider));
}
