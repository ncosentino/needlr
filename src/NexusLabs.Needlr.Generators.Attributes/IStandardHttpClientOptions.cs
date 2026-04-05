namespace NexusLabs.Needlr.Generators;

/// <summary>
/// Convenience aggregate interface that composes all v1 capability interfaces for a
/// named <c>HttpClient</c>: the <see cref="INamedHttpClientOptions"/> marker plus
/// <see cref="IHttpClientTimeout"/>, <see cref="IHttpClientUserAgent"/>,
/// <see cref="IHttpClientBaseAddress"/>, and <see cref="IHttpClientDefaultHeaders"/>.
/// </summary>
/// <remarks>
/// <para>
/// Implementing this single interface is equivalent to implementing each of the
/// capability interfaces individually — the generator emits exactly the same wiring
/// in both cases. Consumers who only need a subset of capabilities can implement
/// just the specific interfaces they care about.
/// </para>
/// <para>
/// New capability interfaces added in future versions of Needlr (resilience,
/// handler chains, handler lifetime, custom delegating handlers, etc.) will NOT be
/// added to this aggregate; they will ship as standalone interfaces so existing
/// consumers of <c>IStandardHttpClientOptions</c> continue to compile unchanged.
/// </para>
/// </remarks>
public interface IStandardHttpClientOptions :
    INamedHttpClientOptions,
    IHttpClientTimeout,
    IHttpClientUserAgent,
    IHttpClientBaseAddress,
    IHttpClientDefaultHeaders
{
}
