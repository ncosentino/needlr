using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

using Xunit;

namespace NexusLabs.Needlr.Generators.Tests;

/// <summary>
/// Tests for <see cref="RecordConstructorOverloadAnalyzer"/> diagnostics.
/// </summary>
public sealed class RecordConstructorOverloadAnalyzerTests
{
    private static string Attributes => NeedlrTestAttributes.AllWithGeneratedConstructor;

    private static CSharpAnalyzerTest<RecordConstructorOverloadAnalyzer, DefaultVerifier> CreateTest(string code) => new()
    {
        TestCode = code + Attributes,
    };

    [Fact]
    public async Task NoDiagnostic_ForValidPositionalRecordAndMarkedProperty()
    {
        var test = CreateTest("""
            #nullable enable
            using NexusLabs.Needlr.Generators;

            public partial record Request(string Name)
            {
                [RecordConstructorOverloadParameter]
                [ConstructorGuard(ConstructorGuardKind.NotNull)]
                public string? Scope { get; init; }
            }
            """);

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NDLRGEN057_WhenRecordIsNotPartial()
    {
        var test = CreateTest("""
            using NexusLabs.Needlr.Generators;

            public record {|#0:Request|}(string Name)
            {
                [RecordConstructorOverloadParameter]
                public int Count { get; init; }
            }
            """);
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN057", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("Request"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NDLRGEN058_WhenMarkerIsUsedOnOrdinaryClass()
    {
        var test = CreateTest("""
            using NexusLabs.Needlr.Generators;

            public partial class {|#0:Request|}
            {
                [RecordConstructorOverloadParameter]
                public int Count { get; set; }
            }
            """);
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN058", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("Request", "an ordinary class rather than a positional record class"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NDLRGEN058_WhenRecordStructIsUsed()
    {
        var test = CreateTest("""
            using NexusLabs.Needlr.Generators;

            public partial record struct {|#0:Request|}(string Name)
            {
                [RecordConstructorOverloadParameter]
                public int Count { get; init; }
            }
            """);
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN058", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("Request", "a record struct"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NDLRGEN058_WhenRecordIsBodyOnly()
    {
        var test = CreateTest("""
            using NexusLabs.Needlr.Generators;

            public partial record {|#0:Request|}
            {
                [RecordConstructorOverloadParameter]
                public int Count { get; init; }
            }
            """);
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN058", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("Request", "a non-positional record with no primary parameter list"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NDLRGEN058_WhenRecordIsNested()
    {
        var test = CreateTest("""
            using NexusLabs.Needlr.Generators;

            public class Container
            {
                public partial record {|#0:Request|}(string Name)
                {
                    [RecordConstructorOverloadParameter]
                    public int Count { get; init; }
                }
            }
            """);
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN058", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("Request", "a nested record"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NDLRGEN058_WhenRecordIsFileLocal()
    {
        var test = CreateTest("""
            using NexusLabs.Needlr.Generators;

            file partial record {|#0:Request|}(string Name)
            {
                [RecordConstructorOverloadParameter]
                public int Count { get; init; }
            }
            """);
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN058", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments(
                    "Request",
                    "a file-local record that cannot be extended from a generated file"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NDLRGEN058_WhenRecordInheritsAnotherRecord()
    {
        var test = CreateTest("""
            using NexusLabs.Needlr.Generators;

            public record BaseRequest(string Name);

            public partial record {|#0:Request|}(string Name, int Version) : BaseRequest(Name)
            {
                [RecordConstructorOverloadParameter]
                public int Count { get; init; }
            }
            """);
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN058", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("Request", "an inherited record"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NDLRGEN059_WhenMarkerTargetsPositionalProperty()
    {
        var test = CreateTest("""
            using NexusLabs.Needlr.Generators;

            public partial record Request(
                [property: {|#0:RecordConstructorOverloadParameter|}] string Name);
            """);
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN059", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("Name", "a positional property synthesized from a primary constructor parameter"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NDLRGEN059_WhenMarkedPropertyIsStatic()
    {
        var test = CreateTest("""
            using NexusLabs.Needlr.Generators;

            public partial record Request(string Name)
            {
                [{|#0:RecordConstructorOverloadParameter|}]
                public static int Count { get; set; }
            }
            """);
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN059", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("Count", "static"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NDLRGEN059_WhenMarkedPropertyIsIndexer()
    {
        var test = CreateTest("""
            using NexusLabs.Needlr.Generators;

            public partial record Request(string Name)
            {
                [{|#0:RecordConstructorOverloadParameter|}]
                public int this[int index]
                {
                    get => index;
                    init { }
                }
            }
            """);
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN059", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("this[]", "an indexer"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NDLRGEN059_WhenMarkedPropertyIsGetOnly()
    {
        var test = CreateTest("""
            using NexusLabs.Needlr.Generators;

            public partial record Request(string Name)
            {
                [{|#0:RecordConstructorOverloadParameter|}]
                public int Count => 1;
            }
            """);
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN059", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("Count", "get-only and cannot be assigned by the generated constructor"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NDLRGEN059_WhenMarkedPropertyIsRequired()
    {
        var test = CreateTest("""
            using NexusLabs.Needlr.Generators;

            public partial record Request(string Name)
            {
                [{|#0:RecordConstructorOverloadParameter|}]
                public required int Count { get; init; }
            }

            namespace System.Runtime.CompilerServices
            {
                [System.AttributeUsage(System.AttributeTargets.All)]
                public sealed class RequiredMemberAttribute : System.Attribute;

                [System.AttributeUsage(System.AttributeTargets.All, AllowMultiple = true)]
                public sealed class CompilerFeatureRequiredAttribute : System.Attribute
                {
                    public CompilerFeatureRequiredAttribute(string featureName)
                    {
                    }
                }
            }

            namespace System.Diagnostics.CodeAnalysis
            {
                [System.AttributeUsage(
                    System.AttributeTargets.Constructor,
                    Inherited = false)]
                public sealed class SetsRequiredMembersAttribute : System.Attribute;
            }
            """);
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN059", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments(
                    "Count",
                    "required; the generated overload does not claim to satisfy the record's complete required-member contract"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NDLRGEN059_WhenPropertyTypeIsLessAccessibleThanGeneratedConstructor()
    {
        var test = CreateTest("""
            using NexusLabs.Needlr.Generators;

            public partial record Request(string Name)
            {
                private sealed class Scope;

                [{|#0:RecordConstructorOverloadParameter|}]
                private Scope? PreparedScope { get; init; }
            }
            """);
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN059", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments(
                    "PreparedScope",
                    "typed as 'Scope?', which is less accessible than the generated public constructor"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NDLRGEN060_WhenGuardTargetsUnmarkedProperty()
    {
        var test = CreateTest("""
            using NexusLabs.Needlr.Generators;

            public partial record Request(string Name)
            {
                [{|#0:ConstructorGuard(ConstructorGuardKind.NotNull)|}]
                public string? Scope { get; init; }
            }
            """);
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN060", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("Scope"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NDLRGEN061_WhenGenerateConstructorIsCombinedWithRecordOverloadMarker()
    {
        var test = CreateTest("""
            using NexusLabs.Needlr.Generators;

            [GenerateConstructor]
            public partial record {|#0:Request|}(string Name)
            {
                [RecordConstructorOverloadParameter]
                public int Count { get; init; }
            }
            """);
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN061", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("Request"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NDLRGEN061_WhenOrdinaryClassCombinesBothConstructorModels()
    {
        var test = CreateTest("""
            using NexusLabs.Needlr.Generators;

            [GenerateConstructor]
            public partial class {|#0:Request|}
            {
                private readonly string _name;

                [RecordConstructorOverloadParameter]
                public int Count { get; init; }
            }
            """);
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN061", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("Request"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NDLRGEN062_WhenGeneratedSignatureCollidesWithCopyConstructor()
    {
        var test = CreateTest("""
            #nullable enable
            using NexusLabs.Needlr.Generators;

            public partial record {|#0:Request|}()
            {
                [RecordConstructorOverloadParameter]
                public Request? Other { get; init; }
            }
            """);
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN062", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("Request", "Request(Request)"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NDLRGEN062_WhenCollisionDiffersOnlyByNullableAnnotation()
    {
        var test = CreateTest("""
            #nullable enable
            using NexusLabs.Needlr.Generators;

            public partial record {|#0:Request|}(string Name)
            {
                [RecordConstructorOverloadParameter]
                public string? Scope { get; init; }

                public Request(string Name, string Scope) : this(Name)
                {
                    this.Scope = Scope;
                }
            }
            """);
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN062", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("Request", "Request(string, string)"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NDLRGEN062_WhenCollisionUsesObjectForDynamicProperty()
    {
        var test = CreateTest("""
            using NexusLabs.Needlr.Generators;

            public partial record {|#0:Request|}(string Name)
            {
                [RecordConstructorOverloadParameter]
                public dynamic Scope { get; init; }

                public Request(string Name, object Scope) : this(Name)
                {
                    this.Scope = Scope;
                }
            }
            """);
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN062", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("Request", "Request(string, object)"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ExistingGuardDiagnostic_IsReusedForIncompatiblePropertyGuard()
    {
        var test = CreateTest("""
            using NexusLabs.Needlr.Generators;

            public partial record Request(string Name)
            {
                [RecordConstructorOverloadParameter]
                [{|#0:ConstructorGuard(ConstructorGuardKind.NotNullOrWhiteSpace)|}]
                public int Count { get; init; }
            }
            """);
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NDLRGEN048", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("NotNullOrWhiteSpace", "Count", "int", "this guard only applies to string-compatible properties"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoDiagnostic_ForParameterizedPropertyAlias()
    {
        var test = CreateTest("""
            #nullable enable
            using NexusLabs.Needlr.Generators;

            public static class MinLengthGuard
            {
                public static void Validate(string? value, int minimum, string parameterName) { }
            }

            [ConstructorGuardDefinition(typeof(MinLengthGuard))]
            [System.AttributeUsage(System.AttributeTargets.Property)]
            public sealed class MinLengthAttribute : System.Attribute
            {
                public MinLengthAttribute(int minimum) { }
            }

            public partial record Request(string Name)
            {
                [RecordConstructorOverloadParameter]
                [MinLength(3)]
                public string? Scope { get; init; }
            }
            """);

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoDiagnostic_ForInternalRecordWithInternalPropertyType()
    {
        var test = CreateTest("""
            using NexusLabs.Needlr.Generators;

            internal sealed class Scope;

            internal partial record Request(string Name)
            {
                [RecordConstructorOverloadParameter]
                internal Scope? PreparedScope { get; init; }
            }
            """);

        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}
