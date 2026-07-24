using System.Linq;

using Xunit;

namespace NexusLabs.Needlr.Generators.Tests;

/// <summary>
/// In-process generator tests for <c>[HttpClientOptions]</c> discovery and emission.
/// </summary>
public sealed class HttpClientOptionsGeneratorTests
{
    [Fact]
    public void AllCapabilities_EmitCompleteNamedClientRegistration()
    {
        var generated = RunGenerator("""
            using System;
            using System.Collections.Generic;
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry]

            namespace TestApp;

            [HttpClientOptions]
            public sealed class WebFetchHttpClientOptions :
                IStandardHttpClientOptions
            {
                public TimeSpan Timeout { get; } = TimeSpan.FromSeconds(15);
                public string? UserAgent { get; } = "needlr-tests";
                public Uri? BaseAddress { get; }
                public IReadOnlyDictionary<string, string>? DefaultHeaders { get; }
            }
            """);

        Assert.Contains(
            "services.AddOptions<global::TestApp.WebFetchHttpClientOptions>().BindConfiguration(\"HttpClients:WebFetch\");",
            generated);
        Assert.Contains(
            "services.AddHttpClient(\"WebFetch\", (sp, client) =>",
            generated);
        Assert.Contains("client.Timeout = options.Timeout;", generated);
        Assert.Contains("if (options.BaseAddress is not null)", generated);
        Assert.Contains(
            "client.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);",
            generated);
        Assert.Contains("foreach (var kvp in options.DefaultHeaders)", generated);
    }

    [Fact]
    public void MarkerOnlyType_EmitsNoCapabilitySpecificWiring()
    {
        var generated = RunGenerator("""
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry]

            namespace TestApp;

            [HttpClientOptions]
            public sealed class MinimalHttpClientOptions :
                INamedHttpClientOptions;
            """);

        Assert.Contains(
            "services.AddHttpClient(\"Minimal\", (sp, client) =>",
            generated);
        Assert.DoesNotContain("client.Timeout =", generated);
        Assert.DoesNotContain("options.BaseAddress", generated);
        Assert.DoesNotContain("options.UserAgent", generated);
        Assert.DoesNotContain("options.DefaultHeaders", generated);
    }

    [Fact]
    public void NameInference_StripsEverySupportedSuffixAndKeepsOtherNames()
    {
        var generated = RunGenerator("""
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry]

            namespace TestApp;

            [HttpClientOptions]
            public sealed class WeatherHttpClientOptions :
                INamedHttpClientOptions;

            [HttpClientOptions]
            public sealed class SearchHttpClientSettings :
                INamedHttpClientOptions;

            [HttpClientOptions]
            public sealed class BillingHttpClient :
                INamedHttpClientOptions;

            [HttpClientOptions]
            public sealed class CustomClientConfiguration :
                INamedHttpClientOptions;
            """);

        Assert.Contains("services.AddHttpClient(\"Weather\"", generated);
        Assert.Contains("services.AddHttpClient(\"Search\"", generated);
        Assert.Contains("services.AddHttpClient(\"Billing\"", generated);
        Assert.Contains(
            "services.AddHttpClient(\"CustomClientConfiguration\"",
            generated);
    }

    [Fact]
    public void ClientNameProperty_AllLiteralSyntaxFormsAreResolved()
    {
        var generated = RunGenerator("""
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry]

            namespace TestApp;

            [HttpClientOptions]
            public sealed class ArrowOptions : INamedHttpClientOptions
            {
                public string ClientName => "arrow";
            }

            [HttpClientOptions]
            public sealed class GetterArrowOptions : INamedHttpClientOptions
            {
                public string ClientName
                {
                    get => "getter-arrow";
                }
            }

            [HttpClientOptions]
            public sealed class GetterBlockOptions : INamedHttpClientOptions
            {
                public string ClientName
                {
                    get
                    {
                        return "getter-block";
                    }
                }
            }
            """);

        Assert.Contains("services.AddHttpClient(\"arrow\"", generated);
        Assert.Contains("services.AddHttpClient(\"getter-arrow\"", generated);
        Assert.Contains("services.AddHttpClient(\"getter-block\"", generated);
    }

    [Fact]
    public void ExplicitNameAndSection_AreEscapedAndEmittedVerbatim()
    {
        var generated = RunGenerator("""
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry]

            namespace TestApp;

            [HttpClientOptions(
                "Upstream:\"Orders\\West",
                Name = "orders\"primary\\west")]
            public sealed class OrdersOptions : INamedHttpClientOptions;
            """);

        Assert.Contains(
            "BindConfiguration(\"Upstream:\\\"Orders\\\\West\")",
            generated);
        Assert.Contains(
            "services.AddHttpClient(\"orders\\\"primary\\\\west\"",
            generated);
    }

    [Fact]
    public void ExactInferenceSuffix_EmitsNoUnnamedRegistration()
    {
        var generated = RunGenerator("""
            [assembly: NexusLabs.Needlr.Generators.GenerateTypeRegistry]

            namespace TestApp;

            [NexusLabs.Needlr.Generators.HttpClientOptions]
            public sealed class HttpClientOptions :
                NexusLabs.Needlr.Generators.INamedHttpClientOptions;
            """);

        Assert.DoesNotContain("services.AddHttpClient(", generated);
    }

    private static string RunGenerator(string source)
    {
        var files = GeneratorTestRunner.ForTypeRegistry()
            .WithSource(source)
            .RunGenerator(new TypeRegistryGenerator());

        return string.Join(
            "\n\n",
            files.Select(file => file.Content));
    }
}
