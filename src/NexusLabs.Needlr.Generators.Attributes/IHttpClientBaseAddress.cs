using System;

namespace NexusLabs.Needlr.Generators;

/// <summary>
/// Capability interface declaring that a named <c>HttpClient</c> options type
/// configures <see cref="System.Net.Http.HttpClient.BaseAddress"/>. When implemented,
/// the source generator emits <c>client.BaseAddress = options.BaseAddress;</c> into
/// the generated <c>AddHttpClient</c> callback, guarded by a null check so a
/// <c>null</c> value leaves the client unchanged.
/// </summary>
public interface IHttpClientBaseAddress
{
    /// <summary>
    /// Gets the base address for the generated <c>HttpClient</c>, or <c>null</c> to
    /// leave it unset.
    /// </summary>
    Uri? BaseAddress { get; }
}
