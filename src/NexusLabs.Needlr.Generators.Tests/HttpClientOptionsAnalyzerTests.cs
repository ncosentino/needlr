using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

using Xunit;

namespace NexusLabs.Needlr.Generators.Tests;

/// <summary>
/// Tests for <see cref="HttpClientOptionsAnalyzer"/>.
/// </summary>
public sealed class HttpClientOptionsAnalyzerTests
{
    private const string Contracts = """
        namespace NexusLabs.Needlr.Generators
        {
            [System.AttributeUsage(
                System.AttributeTargets.Class,
                Inherited = false,
                AllowMultiple = false)]
            public sealed class HttpClientOptionsAttribute : System.Attribute
            {
                public HttpClientOptionsAttribute()
                {
                }

                public HttpClientOptionsAttribute(string sectionName)
                {
                    SectionName = sectionName;
                }

                public string? SectionName { get; }

                public string? Name { get; set; }
            }

            public interface INamedHttpClientOptions
            {
            }
        }
        """;

    [Fact]
    public async Task NoDiagnostic_ForValidExplicitName()
    {
        var test = CreateTest("""
            using NexusLabs.Needlr.Generators;

            [HttpClientOptions(Name = "orders")]
            public sealed class OrdersOptions : INamedHttpClientOptions;
            """);

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoDiagnostic_ForEveryLiteralClientNamePropertyShape()
    {
        var test = CreateTest("""
            using NexusLabs.Needlr.Generators;

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

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoDiagnostic_WhenExplicitNameOverridesComputedProperty()
    {
        var test = CreateTest("""
            using NexusLabs.Needlr.Generators;

            [HttpClientOptions(Name = "explicit")]
            public sealed class ComputedOptions : INamedHttpClientOptions
            {
                public string ClientName => GetName();

                private static string GetName() => "computed";
            }
            """);

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NDLRHTTP001_WhenMarkerInterfaceIsMissing()
    {
        var test = CreateTest("""
            using NexusLabs.Needlr.Generators;

            [{|#0:HttpClientOptions(Name = "orders")|}]
            public sealed class OrdersOptions;
            """);
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRHTTP001", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("OrdersOptions"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NDLRHTTP002_WhenAttributeAndPropertyNamesConflict()
    {
        var test = CreateTest("""
            using NexusLabs.Needlr.Generators;

            [{|#0:HttpClientOptions(Name = "attribute-name")|}]
            public sealed class OrdersOptions : INamedHttpClientOptions
            {
                public string ClientName => "property-name";
            }
            """);
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRHTTP002", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments(
                    "OrdersOptions",
                    "attribute-name",
                    "property-name"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NDLRHTTP003_WhenClientNamePropertyIsComputed()
    {
        var test = CreateTest("""
            using NexusLabs.Needlr.Generators;

            [HttpClientOptions]
            public sealed class OrdersOptions : INamedHttpClientOptions
            {
                public string {|#0:ClientName|} => GetName();

                private static string GetName() => "orders";
            }
            """);
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRHTTP003", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("OrdersOptions"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NDLRHTTP004_WhenTypeNameIsOnlyAnInferenceSuffix()
    {
        var test = CreateTest("""
            [{|#0:NexusLabs.Needlr.Generators.HttpClientOptions|}]
            public sealed class HttpClientOptions :
                NexusLabs.Needlr.Generators.INamedHttpClientOptions;
            """);
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRHTTP004", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("HttpClientOptions"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NDLRHTTP004_WhenTypeNameIsHttpClientSettingsSuffix()
    {
        var test = CreateTest("""
            [{|#0:NexusLabs.Needlr.Generators.HttpClientOptions|}]
            public sealed class HttpClientSettings :
                NexusLabs.Needlr.Generators.INamedHttpClientOptions;
            """);
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRHTTP004", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("HttpClientSettings"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NDLRHTTP004_WhenTypeNameIsHttpClientSuffix()
    {
        var test = CreateTest("""
            [{|#0:NexusLabs.Needlr.Generators.HttpClientOptions|}]
            public sealed class HttpClient :
                NexusLabs.Needlr.Generators.INamedHttpClientOptions;
            """);
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRHTTP004", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("HttpClient"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoDiagnostic_WhenExactSuffixTypeHasExplicitName()
    {
        var test = CreateTest("""
            [NexusLabs.Needlr.Generators.HttpClientOptions(Name = "default")]
            public sealed class HttpClientOptions :
                NexusLabs.Needlr.Generators.INamedHttpClientOptions;
            """);

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NDLRHTTP005_WhenTwoTypesResolveToSameName()
    {
        var test = CreateTest("""
            using NexusLabs.Needlr.Generators;

            [HttpClientOptions(Name = "shared")]
            public sealed class FirstOptions : INamedHttpClientOptions;

            [{|#0:HttpClientOptions(Name = "shared")|}]
            public sealed class SecondOptions : INamedHttpClientOptions;
            """);
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRHTTP005", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("SecondOptions", "shared", "FirstOptions"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NDLRHTTP006_WhenClientNamePropertyHasWrongShape()
    {
        var test = CreateTest("""
            using NexusLabs.Needlr.Generators;

            [HttpClientOptions]
            public sealed class OrdersOptions : INamedHttpClientOptions
            {
                public static string {|#0:ClientName|} => "orders";
            }
            """);
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRHTTP006", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("OrdersOptions"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    private static CSharpAnalyzerTest<
        HttpClientOptionsAnalyzer,
        DefaultVerifier> CreateTest(string source)
    {
        return new CSharpAnalyzerTest<
            HttpClientOptionsAnalyzer,
            DefaultVerifier>
        {
            TestCode = source + Contracts,
        };
    }
}
