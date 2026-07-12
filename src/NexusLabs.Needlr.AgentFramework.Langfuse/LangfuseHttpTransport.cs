namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Owns the HTTP transport shared by all Langfuse REST API clients in one client composition.
/// </summary>
internal sealed class LangfuseHttpTransport : IDisposable
{
    private readonly HttpClient _httpClient;

    public LangfuseHttpTransport()
        : this(new HttpClient())
    {
    }

    public LangfuseHttpTransport(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
    }

    public HttpClient HttpClient => _httpClient;

    public void Dispose() => _httpClient.Dispose();
}
