using System;

namespace NexusLabs.Needlr.Generators;

/// <summary>
/// Capability interface declaring that a named <c>HttpClient</c> options type
/// configures <see cref="System.Net.Http.HttpClient.Timeout"/>. When implemented,
/// the source generator emits <c>client.Timeout = options.Timeout;</c> into the
/// generated <c>AddHttpClient</c> callback.
/// </summary>
public interface IHttpClientTimeout
{
    /// <summary>
    /// Gets the per-request timeout applied to the generated <c>HttpClient</c>.
    /// </summary>
    TimeSpan Timeout { get; }
}
