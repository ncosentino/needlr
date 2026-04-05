using System;
using System.Collections.Generic;

using NexusLabs.Needlr.Generators;

namespace HttpClientExample;

/// <summary>
/// Demonstrates the happy path: name inferred from the type-name suffix ("WebFetch"),
/// section inferred as "HttpClients:WebFetch", all v1 capabilities opted in via the
/// aggregate interface. Defaults come from the property initializers; operators override
/// any subset in appsettings.json without a rebuild.
/// </summary>
[HttpClientOptions]
public sealed record WebFetchHttpClientOptions : IStandardHttpClientOptions
{
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(15);
    public string? UserAgent { get; init; } = "HttpClientExample/1.0";
    public Uri? BaseAddress { get; init; }
    public IReadOnlyDictionary<string, string>? DefaultHeaders { get; init; }
}

/// <summary>
/// Demonstrates the attribute-argument name source (highest precedence). Even though
/// the type name would suggest "BravePrimary", the attribute forces "brave" as the
/// HttpClient name.
/// </summary>
[HttpClientOptions(Name = "brave")]
public sealed record BravePrimaryHttpClientOptions : IStandardHttpClientOptions
{
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(15);
    public string? UserAgent { get; init; } = "HttpClientExample/1.0";
    public Uri? BaseAddress { get; init; } = new Uri("https://search.brave.com/");
    public IReadOnlyDictionary<string, string>? DefaultHeaders { get; init; }
}

/// <summary>
/// Demonstrates explicit section path with colons (not the inferred HttpClients:&lt;Name&gt;
/// form) plus a minimal capability set — only Timeout, no UserAgent / BaseAddress /
/// Headers wiring emitted.
/// </summary>
[HttpClientOptions("Upstream:Tavily")]
public sealed record TavilyHttpClientOptions : INamedHttpClientOptions, IHttpClientTimeout
{
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(10);
}
