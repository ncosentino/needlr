namespace NexusLabs.Needlr.AgentFramework.Langfuse.Tests;

/// <summary>
/// HTTP boundary helper that delegates asynchronous responses to a test callback.
/// </summary>
internal sealed class DelegateHttpMessageHandler(
    Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync)
    : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) =>
        sendAsync(request, cancellationToken);
}
