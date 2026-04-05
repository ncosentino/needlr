namespace NexusLabs.Needlr.Generators;

/// <summary>
/// Marker interface for types decorated with <see cref="HttpClientOptionsAttribute"/>.
/// Every <c>[HttpClientOptions]</c> target must implement this interface; the
/// <c>NDLRHTTP001</c> analyzer diagnostic enforces it at compile time.
/// </summary>
/// <remarks>
/// <para>
/// The interface is intentionally empty. The actual configurability of a named
/// <c>HttpClient</c> is composed from additional capability interfaces
/// (<c>IHttpClient*</c>), which the source generator detects independently and
/// emits wiring for only when implemented.
/// </para>
/// <para>
/// Keeping the client name OFF this interface is deliberate — the name is resolved
/// by the generator from one of three sources (attribute argument, <c>ClientName</c>
/// property on the type, or inferred from the type name), and forcing every consumer
/// to declare it in the same place would make the inference and attribute-override
/// paths impossible.
/// </para>
/// </remarks>
public interface INamedHttpClientOptions
{
}
