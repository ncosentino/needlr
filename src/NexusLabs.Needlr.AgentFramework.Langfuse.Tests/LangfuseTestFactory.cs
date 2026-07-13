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
        var httpClient = LangfuseHttpStub.Create(_ => new HttpResponseMessage(HttpStatusCode.OK), list);
        var apiClient = new LangfuseScoreApiClient(
            httpClient,
            new Uri("https://lf.example/api/public/scores"),
            "Basic x");
        return new LangfuseScoreRecorder(
            apiClient,
            new LangfuseScoreFailureSink(LangfuseScoreFailureMode.Strict, null),
            normalizeNames: false);
    }
}
