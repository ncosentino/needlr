using System.Net;
using System.Text.Json;

namespace NexusLabs.Needlr.AgentFramework.Langfuse.Tests;

/// <summary>
/// Test helper that wires an HTTP boundary so tests can drive the real Langfuse client code and
/// assert on the requests it emits.
/// </summary>
internal static class LangfuseHttpStub
{
    public static HttpClient Create(
        Func<HttpRequestMessage, HttpResponseMessage> responder,
        List<CapturedRequest> captured)
    {
        var handler = new DelegateHttpMessageHandler(
            async (request, token) =>
            {
                var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(token);
                captured.Add(new CapturedRequest(request.Method, request.RequestUri!, body));
                return responder(request);
            });

        return new HttpClient(handler);
    }

    public static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json) };

    public static HttpResponseMessage ScoreAccepted(HttpRequestMessage request)
    {
        ArgumentNullException.ThrowIfNull(request.Content);
        using var json = JsonDocument.Parse(request.Content.ReadAsStream());
        var scoreId = json.RootElement.TryGetProperty("id", out var id)
            ? id.GetString()
            : "generated-score-id";
        return Json(
            HttpStatusCode.OK,
            JsonSerializer.Serialize(new { id = scoreId }));
    }
}
