using System.Net;

using Moq;
using Moq.Protected;

namespace NexusLabs.Needlr.AgentFramework.Langfuse.Tests;

/// <summary>A single HTTP request observed by <see cref="LangfuseHttpStub"/>.</summary>
internal sealed record CapturedRequest(HttpMethod Method, Uri Uri, string? Body);

/// <summary>
/// Test helper that wires a mocked <see cref="HttpMessageHandler"/> (an HTTP boundary) so tests can
/// drive the real Langfuse client code and assert on the requests it emits.
/// </summary>
internal static class LangfuseHttpStub
{
    public static HttpClient Create(
        Func<HttpRequestMessage, HttpResponseMessage> responder,
        List<CapturedRequest> captured)
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
                captured.Add(new CapturedRequest(request.Method, request.RequestUri!, body));
                return responder(request);
            });

        return new HttpClient(handler.Object, disposeHandler: false);
    }

    public static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json) };
}
