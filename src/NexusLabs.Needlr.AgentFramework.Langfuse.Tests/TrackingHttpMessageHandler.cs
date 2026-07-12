using System.Net;

namespace NexusLabs.Needlr.AgentFramework.Langfuse.Tests;

/// <summary>
/// HTTP boundary helper that records whether its owning <see cref="HttpClient"/> released it.
/// </summary>
internal sealed class TrackingHttpMessageHandler : HttpMessageHandler
{
    private readonly List<CapturedRequest> _capturedRequests = [];
    private int _disposeCalls;

    public IReadOnlyList<CapturedRequest> CapturedRequests
    {
        get
        {
            lock (_capturedRequests)
            {
                return [.. _capturedRequests];
            }
        }
    }

    public int DisposeCalls => Volatile.Read(ref _disposeCalls);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var body = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);
        lock (_capturedRequests)
        {
            _capturedRequests.Add(new CapturedRequest(
                request.Method,
                request.RequestUri!,
                body));
        }

        return new HttpResponseMessage(HttpStatusCode.OK);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Interlocked.Increment(ref _disposeCalls);
        }

        base.Dispose(disposing);
    }
}
