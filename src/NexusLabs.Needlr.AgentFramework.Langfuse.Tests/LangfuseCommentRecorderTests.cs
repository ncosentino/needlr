using System.Net;
using System.Text.Json;

namespace NexusLabs.Needlr.AgentFramework.Langfuse.Tests;

public sealed class LangfuseCommentRecorderTests
{
    private static readonly Uri BaseUrl = new("https://lf.example/");

    [Fact]
    public async Task AddTraceCommentAsync_ResolvesProjectThenPostsTraceComment()
    {
        var captured = new List<CapturedRequest>();
        using var httpClient = LangfuseHttpStub.Create(
            request => request.RequestUri!.AbsolutePath.EndsWith("/projects", StringComparison.Ordinal)
                ? LangfuseHttpStub.Json(HttpStatusCode.OK, "{\"data\":[{\"id\":\"proj-1\"}]}")
                : new HttpResponseMessage(HttpStatusCode.Created),
            captured);

        var recorder = new LangfuseCommentRecorder(new LangfuseApiClient(httpClient, BaseUrl, "Basic x"), diagnostics: null);

        await recorder.AddTraceCommentAsync("trace-9", "CI run https://ci/42", TestContext.Current.CancellationToken);

        var post = Assert.Single(captured, c => c.Method == HttpMethod.Post);
        Assert.EndsWith("/api/public/comments", post.Uri.AbsolutePath, StringComparison.Ordinal);
        using var json = JsonDocument.Parse(post.Body!);
        Assert.Equal("proj-1", json.RootElement.GetProperty("projectId").GetString());
        Assert.Equal("TRACE", json.RootElement.GetProperty("objectType").GetString());
        Assert.Equal("trace-9", json.RootElement.GetProperty("objectId").GetString());
        Assert.Equal("CI run https://ci/42", json.RootElement.GetProperty("content").GetString());
    }

    [Fact]
    public async Task AddTraceCommentAsync_CachesProjectIdAcrossCalls()
    {
        var captured = new List<CapturedRequest>();
        using var httpClient = LangfuseHttpStub.Create(
            request => request.RequestUri!.AbsolutePath.EndsWith("/projects", StringComparison.Ordinal)
                ? LangfuseHttpStub.Json(HttpStatusCode.OK, "{\"data\":[{\"id\":\"proj-1\"}]}")
                : new HttpResponseMessage(HttpStatusCode.Created),
            captured);

        var recorder = new LangfuseCommentRecorder(new LangfuseApiClient(httpClient, BaseUrl, "Basic x"), diagnostics: null);

        await recorder.AddTraceCommentAsync("trace-1", "a", TestContext.Current.CancellationToken);
        await recorder.AddTraceCommentAsync("trace-2", "b", TestContext.Current.CancellationToken);

        Assert.Single(captured, c => c.Uri.AbsolutePath.EndsWith("/projects", StringComparison.Ordinal));
        Assert.Equal(2, captured.Count(c => c.Uri.AbsolutePath.EndsWith("/comments", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task AddTraceCommentAsync_OnFailure_IsNonFatalAndReportsDiagnostic()
    {
        var captured = new List<CapturedRequest>();
        using var httpClient = LangfuseHttpStub.Create(
            request => request.RequestUri!.AbsolutePath.EndsWith("/projects", StringComparison.Ordinal)
                ? LangfuseHttpStub.Json(HttpStatusCode.OK, "{\"data\":[{\"id\":\"proj-1\"}]}")
                : LangfuseHttpStub.Json(HttpStatusCode.BadRequest, "bad"),
            captured);

        string? diagnostic = null;
        var recorder = new LangfuseCommentRecorder(new LangfuseApiClient(httpClient, BaseUrl, "Basic x"), d => diagnostic = d);

        await recorder.AddTraceCommentAsync("trace-9", "x", TestContext.Current.CancellationToken);

        Assert.NotNull(diagnostic);
        Assert.Contains("trace-9", diagnostic!, StringComparison.Ordinal);
    }
}
