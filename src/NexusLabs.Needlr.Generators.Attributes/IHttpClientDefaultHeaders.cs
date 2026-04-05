using System.Collections.Generic;

namespace NexusLabs.Needlr.Generators;

/// <summary>
/// Capability interface declaring that a named <c>HttpClient</c> options type
/// configures arbitrary default request headers. When implemented, the source
/// generator emits a foreach loop into the generated <c>AddHttpClient</c> callback
/// that calls <c>client.DefaultRequestHeaders.Add(kvp.Key, kvp.Value)</c> for each
/// entry, guarded by a null check on the dictionary.
/// </summary>
/// <remarks>
/// Use this for headers that are safe to treat as <c>Add</c> (not <c>TryAddWithoutValidation</c>).
/// The <c>User-Agent</c> header specifically should be expressed via
/// <see cref="IHttpClientUserAgent"/> because it uses a different setter
/// (<c>UserAgent.ParseAdd</c>).
/// </remarks>
public interface IHttpClientDefaultHeaders
{
    /// <summary>
    /// Gets the default request headers to add to the generated <c>HttpClient</c>,
    /// or <c>null</c> to leave the defaults unchanged.
    /// </summary>
    IReadOnlyDictionary<string, string>? DefaultHeaders { get; }
}
