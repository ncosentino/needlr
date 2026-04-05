using System;

namespace NexusLabs.Needlr.Generators;

/// <summary>
/// Marks a class as a named <c>HttpClient</c> configuration type. The source generator
/// will emit both an <c>AddOptions&lt;T&gt;().BindConfiguration(...)</c> call and a
/// matching <c>services.AddHttpClient(name, (sp, client) =&gt; { ... })</c> registration,
/// so consumers never have to hand-write either one.
/// </summary>
/// <remarks>
/// <para>
/// The decorated type MUST implement <see cref="INamedHttpClientOptions"/>. It opts into
/// additional configurability by implementing capability interfaces such as
/// <see cref="IHttpClientTimeout"/>, <see cref="IHttpClientUserAgent"/>,
/// <see cref="IHttpClientBaseAddress"/>, and <see cref="IHttpClientDefaultHeaders"/>.
/// The generator emits only the wiring for capabilities actually implemented, so there
/// is no dead code and no attribute property churn when new capabilities are added.
/// </para>
/// <para>
/// The resolved HttpClient name comes from, in order of precedence:
/// <list type="number">
/// <item><description>The attribute's <see cref="Name"/> property, if set.</description></item>
/// <item><description>A <c>ClientName</c> property on the decorated type with a literal expression body (e.g. <c>public string ClientName =&gt; "WebFetch";</c>).</description></item>
/// <item><description>Inferred from the type name by stripping the suffixes <c>HttpClientOptions</c>, <c>HttpClientSettings</c>, or <c>HttpClient</c>.</description></item>
/// </list>
/// </para>
/// <para>
/// The resolved configuration section comes from the attribute's constructor argument, or
/// is inferred as <c>"HttpClients:&lt;ResolvedName&gt;"</c> when no section is given.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Minimal form — name inferred as "WebFetch", section inferred as "HttpClients:WebFetch"
/// [HttpClientOptions]
/// public sealed record WebFetchHttpClientOptions : IStandardHttpClientOptions
/// {
///     public string    ClientName     =&gt; "WebFetch"; // optional when suffix-stripping works
///     public TimeSpan  Timeout        { get; init; } = TimeSpan.FromSeconds(15);
///     public string?   UserAgent      { get; init; } = "BrandGhost-Agent/1.0";
///     public Uri?      BaseAddress    { get; init; }
///     public IReadOnlyDictionary&lt;string, string&gt;? DefaultHeaders { get; init; }
/// }
///
/// // Explicit section
/// [HttpClientOptions("Upstream:Tavily")]
/// public sealed record TavilyHttpClientOptions : IStandardHttpClientOptions { ... }
///
/// // Explicit name override
/// [HttpClientOptions(Name = "tavily-primary")]
/// public sealed record TavilyPrimaryHttpClientOptions : IStandardHttpClientOptions { ... }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class HttpClientOptionsAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HttpClientOptionsAttribute"/> class
    /// with the section name inferred from the resolved client name
    /// (<c>"HttpClients:&lt;ResolvedName&gt;"</c>).
    /// </summary>
    public HttpClientOptionsAttribute()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpClientOptionsAttribute"/> class
    /// with an explicit configuration section name.
    /// </summary>
    /// <param name="sectionName">
    /// The configuration section to bind to (e.g., <c>"HttpClients:WebFetch"</c> or
    /// <c>"Upstream:Tavily"</c>).
    /// </param>
    public HttpClientOptionsAttribute(string sectionName)
    {
        SectionName = sectionName;
    }

    /// <summary>
    /// Gets the explicit configuration section name, or <c>null</c> to infer it from the
    /// resolved client name.
    /// </summary>
    public string? SectionName { get; }

    /// <summary>
    /// Gets or sets the explicit HttpClient name. When set, this overrides both the
    /// <c>ClientName</c> property on the decorated type and the type-name inference
    /// fallback.
    /// </summary>
    public string? Name { get; set; }
}
