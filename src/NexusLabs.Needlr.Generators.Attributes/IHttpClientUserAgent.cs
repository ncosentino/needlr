namespace NexusLabs.Needlr.Generators;

/// <summary>
/// Capability interface declaring that a named <c>HttpClient</c> options type
/// configures the <c>User-Agent</c> default request header. When implemented, the
/// source generator emits a
/// <c>client.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent)</c> call
/// into the generated <c>AddHttpClient</c> callback, guarded by a null/empty check.
/// </summary>
public interface IHttpClientUserAgent
{
    /// <summary>
    /// Gets the <c>User-Agent</c> string to apply to the generated <c>HttpClient</c>,
    /// or <c>null</c> to leave the default.
    /// </summary>
    string? UserAgent { get; }
}
