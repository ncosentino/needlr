using System.Net;

namespace NexusLabs.Needlr.AgentFramework.Langfuse.Tests;

/// <summary>
/// HTTP boundary helper that records whether its owning <see cref="HttpClient"/> released it.
/// </summary>
internal sealed class TrackingHttpMessageHandler : HttpMessageHandler
{
    private int _disposeCalls;

    public int DisposeCalls => Volatile.Read(ref _disposeCalls);

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Interlocked.Increment(ref _disposeCalls);
        }

        base.Dispose(disposing);
    }
}
