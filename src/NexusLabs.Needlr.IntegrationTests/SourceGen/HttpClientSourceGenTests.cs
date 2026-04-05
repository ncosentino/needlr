using System;
using System.Collections.Generic;
using System.Net.Http;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using NexusLabs.Needlr.Generators;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.SourceGen;

using Xunit;

namespace NexusLabs.Needlr.IntegrationTests.SourceGen;

/// <summary>
/// Integration tests for [HttpClientOptions] source generation. These tests verify the
/// generated code actually wires named HttpClients into a real DI container using the
/// Syringe fluent API — the same shape the existing OptionsSourceGenTests.cs uses.
/// </summary>
public sealed class HttpClientSourceGenTests
{
    private static IServiceProvider BuildProvider(IConfiguration configuration)
    {
        return new Syringe()
            .UsingGeneratedComponents(
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetInjectableTypes,
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes)
            .BuildServiceProvider(configuration);
    }

    [Fact]
    public void HttpClient_UsesDefaults_WhenNoConfigurationSectionPresent()
    {
        var provider = BuildProvider(new ConfigurationBuilder().Build());

        var factory = provider.GetRequiredService<IHttpClientFactory>();
        var client = factory.CreateClient("WebFetch");

        Assert.Equal(TimeSpan.FromSeconds(15), client.Timeout);
        Assert.Contains("BrandGhost-Agent/1.0", client.DefaultRequestHeaders.UserAgent.ToString());
    }

    [Fact]
    public void HttpClient_AppliesConfigurationOverrides_FromAppSettings()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["HttpClients:WebFetch:Timeout"] = "00:00:42",
                ["HttpClients:WebFetch:UserAgent"] = "override-ua",
            })
            .Build();

        var provider = BuildProvider(configuration);

        var factory = provider.GetRequiredService<IHttpClientFactory>();
        var client = factory.CreateClient("WebFetch");

        Assert.Equal(TimeSpan.FromSeconds(42), client.Timeout);
        Assert.Contains("override-ua", client.DefaultRequestHeaders.UserAgent.ToString());
    }

    [Fact]
    public void HttpClient_CanAlsoBeResolvedAsIOptionsT_ForRuntimeAccess()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["HttpClients:WebFetch:Timeout"] = "00:00:25",
            })
            .Build();

        var provider = BuildProvider(configuration);

        var options = provider.GetRequiredService<IOptions<WebFetchHttpClientOptions>>();

        Assert.NotNull(options);
        Assert.Equal(TimeSpan.FromSeconds(25), options.Value.Timeout);
        Assert.Equal("BrandGhost-Agent/1.0", options.Value.UserAgent);
    }

    [Fact]
    public void HttpClient_WithBaseAddress_AppliesWhenNonNull()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["HttpClients:Brave:BaseAddress"] = "https://brave.example.com/",
            })
            .Build();

        var provider = BuildProvider(configuration);

        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("Brave");

        Assert.Equal(new Uri("https://brave.example.com/"), client.BaseAddress);
    }

    [Fact]
    public void HttpClient_NameSource_AttributeArgument_Wins()
    {
        // This type sets [HttpClientOptions(Name = "tavily-primary")] — the attribute arg
        // is the highest-precedence name source, even though the type name would suggest
        // "Tavily" via suffix-stripping.
        var provider = BuildProvider(new ConfigurationBuilder().Build());
        var factory = provider.GetRequiredService<IHttpClientFactory>();

        // The client registered under the attribute-supplied name must exist and have its
        // own configured defaults.
        var client = factory.CreateClient("tavily-primary");
        Assert.Equal(TimeSpan.FromSeconds(20), client.Timeout);
    }

    [Fact]
    public void HttpClient_NameSource_ClientNameProperty_Wins_OverTypeNameInference()
    {
        // HttpClientTypeNameSuffixStrippingOptions has a ClientName property returning
        // "explicit-from-property". Without the property, suffix stripping would produce
        // "HttpClientTypeNameSuffixStripping".
        var provider = BuildProvider(new ConfigurationBuilder().Build());
        var factory = provider.GetRequiredService<IHttpClientFactory>();

        var client = factory.CreateClient("explicit-from-property");
        Assert.Equal(TimeSpan.FromSeconds(11), client.Timeout);
    }

    [Fact]
    public void HttpClient_NameSource_TypeNameInference_Fallback()
    {
        // ExaHttpClient has no attribute Name override and no ClientName property — name
        // must come from suffix-stripping: ExaHttpClient -> "Exa".
        var provider = BuildProvider(new ConfigurationBuilder().Build());
        var factory = provider.GetRequiredService<IHttpClientFactory>();

        var client = factory.CreateClient("Exa");
        Assert.Equal(TimeSpan.FromSeconds(13), client.Timeout);
    }

    [Fact]
    public void HttpClient_SectionName_InferredFromResolvedClientName_WhenAttributeSectionAbsent()
    {
        // ExaHttpClient uses section inference: section should be "HttpClients:Exa".
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["HttpClients:Exa:Timeout"] = "00:00:33",
            })
            .Build();

        var provider = BuildProvider(configuration);
        var options = provider.GetRequiredService<IOptions<ExaHttpClient>>();

        Assert.Equal(TimeSpan.FromSeconds(33), options.Value.Timeout);
    }

    [Fact]
    public void HttpClient_WithOnlyTimeoutCapability_DoesNotEmitUserAgentWiring()
    {
        // MinimalTimeoutOnlyHttpClientOptions implements only IHttpClientTimeout. The
        // generator must not emit UserAgent/BaseAddress/Headers wiring, and the resulting
        // HttpClient should have default UserAgent (unset), default BaseAddress (null),
        // and no extra headers — confirming the capability-driven emission works.
        var provider = BuildProvider(new ConfigurationBuilder().Build());
        var factory = provider.GetRequiredService<IHttpClientFactory>();

        var client = factory.CreateClient("MinimalTimeoutOnly");

        Assert.Equal(TimeSpan.FromSeconds(7), client.Timeout);
        Assert.Null(client.BaseAddress);
        // Default HttpClient has no User-Agent; if the generator emitted wiring despite the
        // capability being absent, this would be non-empty.
        Assert.Empty(client.DefaultRequestHeaders.UserAgent);
    }

    [Fact]
    public void HttpClient_WithCustomSectionPath_BindsCorrectly()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Upstream:DuckDuckGo:Timeout"] = "00:00:19",
                ["Upstream:DuckDuckGo:UserAgent"] = "ddg-ua",
            })
            .Build();

        var provider = BuildProvider(configuration);
        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("DuckDuckGoHtml");

        Assert.Equal(TimeSpan.FromSeconds(19), client.Timeout);
        Assert.Contains("ddg-ua", client.DefaultRequestHeaders.UserAgent.ToString());
    }

    [Fact]
    public void HttpClient_WithDeeplyNestedSectionPath_BindsCorrectly()
    {
        // 4 colons deep. Proves there's no depth limit on the section path — the generator
        // passes the attribute arg verbatim to BindConfiguration, which delegates to
        // IConfiguration.GetSection, which treats ':' as an unbounded path separator.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FeatureC:FeatureB:FeatureA:ExternalApi:Timeout"] = "00:00:45",
                ["FeatureC:FeatureB:FeatureA:ExternalApi:UserAgent"] = "deeply-nested-ua",
                ["FeatureC:FeatureB:FeatureA:ExternalApi:BaseAddress"] = "https://deeply.nested.example.com/",
            })
            .Build();

        var provider = BuildProvider(configuration);
        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("DeeplyNested");

        Assert.Equal(TimeSpan.FromSeconds(45), client.Timeout);
        Assert.Contains("deeply-nested-ua", client.DefaultRequestHeaders.UserAgent.ToString());
        Assert.Equal(new Uri("https://deeply.nested.example.com/"), client.BaseAddress);
    }

    [Fact]
    public void HttpClient_DefaultHeaders_AreAppliedToClient()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["HttpClients:HeaderBearing:DefaultHeaders:X-Api-Key"] = "secret",
                ["HttpClients:HeaderBearing:DefaultHeaders:X-Trace-Id"] = "trace-123",
            })
            .Build();

        var provider = BuildProvider(configuration);
        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("HeaderBearing");

        Assert.True(client.DefaultRequestHeaders.TryGetValues("X-Api-Key", out var apiKeyValues));
        Assert.Contains("secret", apiKeyValues!);

        Assert.True(client.DefaultRequestHeaders.TryGetValues("X-Trace-Id", out var traceValues));
        Assert.Contains("trace-123", traceValues!);
    }
}

// ------------------------------------------------------------------
// Test options types — co-located per house style (mirrors OptionsSourceGenTests.cs).
// ------------------------------------------------------------------

/// <summary>
/// Primary happy-path options: section inferred as "HttpClients:WebFetch" (from the type
/// name via suffix stripping), implements all v1 capabilities via the aggregate interface.
/// </summary>
[HttpClientOptions]
public sealed record WebFetchHttpClientOptions : IStandardHttpClientOptions
{
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(15);
    public string? UserAgent { get; init; } = "BrandGhost-Agent/1.0";
    public Uri? BaseAddress { get; init; }
    public IReadOnlyDictionary<string, string>? DefaultHeaders { get; init; }
}

/// <summary>
/// Verifies BaseAddress wiring. Name inferred as "Brave" from the suffix-stripped type name.
/// </summary>
[HttpClientOptions]
public sealed record BraveHttpClientOptions : IStandardHttpClientOptions
{
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(15);
    public string? UserAgent { get; init; }
    public Uri? BaseAddress { get; init; }
    public IReadOnlyDictionary<string, string>? DefaultHeaders { get; init; }
}

/// <summary>
/// Verifies attribute-name precedence. Suffix stripping would yield "TavilyPrimary" but the
/// attribute arg wins.
/// </summary>
[HttpClientOptions(Name = "tavily-primary")]
public sealed record TavilyPrimaryHttpClientOptions : IStandardHttpClientOptions
{
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(20);
    public string? UserAgent { get; init; }
    public Uri? BaseAddress { get; init; }
    public IReadOnlyDictionary<string, string>? DefaultHeaders { get; init; }
}

/// <summary>
/// Verifies the ClientName-property name source (middle precedence). No attribute arg, but a
/// literal ClientName property takes precedence over suffix-stripping.
/// </summary>
[HttpClientOptions]
public sealed record HttpClientTypeNameSuffixStrippingOptions : IStandardHttpClientOptions
{
    public string ClientName => "explicit-from-property";
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(11);
    public string? UserAgent { get; init; }
    public Uri? BaseAddress { get; init; }
    public IReadOnlyDictionary<string, string>? DefaultHeaders { get; init; }
}

/// <summary>
/// Verifies type-name inference fallback: no attribute arg, no ClientName property. Name
/// should be "Exa" (stripped "HttpClient" suffix).
/// </summary>
[HttpClientOptions]
public sealed record ExaHttpClient : IStandardHttpClientOptions
{
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(13);
    public string? UserAgent { get; init; }
    public Uri? BaseAddress { get; init; }
    public IReadOnlyDictionary<string, string>? DefaultHeaders { get; init; }
}

/// <summary>
/// Verifies the "no dead wiring" guarantee: this type opts into ONLY the Timeout capability.
/// The generator must not emit UserAgent/BaseAddress/Headers code for this client.
/// </summary>
[HttpClientOptions]
public sealed record MinimalTimeoutOnlyHttpClientOptions : INamedHttpClientOptions, IHttpClientTimeout
{
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(7);
}

/// <summary>
/// Verifies explicit section path with colons (not the inferred HttpClients:&lt;Name&gt; form).
/// </summary>
[HttpClientOptions("Upstream:DuckDuckGo")]
public sealed record DuckDuckGoHtmlHttpClientOptions : IStandardHttpClientOptions
{
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(15);
    public string? UserAgent { get; init; }
    public Uri? BaseAddress { get; init; }
    public IReadOnlyDictionary<string, string>? DefaultHeaders { get; init; }
}

/// <summary>
/// Verifies the DefaultHeaders capability binding from a nested config section.
/// </summary>
[HttpClientOptions(Name = "HeaderBearing")]
public sealed record HeaderBearingHttpClientOptions : INamedHttpClientOptions, IHttpClientDefaultHeaders
{
    public IReadOnlyDictionary<string, string>? DefaultHeaders { get; init; }
}

/// <summary>
/// Verifies 4-level nested section path binding. Section string is passed verbatim to
/// BindConfiguration, which delegates to IConfiguration.GetSection — colons are the
/// standard unbounded path separator, so arbitrary feature-nesting depths work without
/// any generator-side special-casing. Name inferred as "DeeplyNested" via suffix strip.
/// </summary>
[HttpClientOptions("FeatureC:FeatureB:FeatureA:ExternalApi")]
public sealed record DeeplyNestedHttpClientOptions : IStandardHttpClientOptions
{
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(5);
    public string? UserAgent { get; init; }
    public Uri? BaseAddress { get; init; }
    public IReadOnlyDictionary<string, string>? DefaultHeaders { get; init; }
}
