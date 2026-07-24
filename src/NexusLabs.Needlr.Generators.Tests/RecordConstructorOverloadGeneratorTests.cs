using System.Linq;

using Xunit;

namespace NexusLabs.Needlr.Generators.Tests;

/// <summary>
/// Behavioral tests for <see cref="RecordConstructorOverloadGenerator"/>.
/// </summary>
public sealed class RecordConstructorOverloadGeneratorTests
{
    [Fact]
    public void VeritasShapedRecord_EmitsForwardingConstructorGuardAndAssignment()
    {
        var source = """
            #nullable enable
            using NexusLabs.Needlr.Generators;

            namespace TestApp;

            public sealed class PreparedScope;

            /// <summary>A prepared query request.</summary>
            /// <param name="Query">The query text.</param>
            /// <param name="Tenant">The tenant identifier.</param>
            public partial record VerificationRequest(
                string Query,
                string Tenant,
                int Limit,
                bool IncludeDrafts,
                global::System.Guid CorrelationId,
                global::System.DateTimeOffset RequestedAt,
                global::System.Collections.Generic.IReadOnlyList<string> Tags)
            {
                /// <summary>The prepared execution scope.</summary>
                [RecordConstructorOverloadParameter]
                [ConstructorGuard(ConstructorGuardKind.NotNull)]
                public PreparedScope? PreparedScope { get; init; }
            }
            """;

        var generatedCode = RunGenerator(source);

        Assert.Contains(
            "public VerificationRequest(string Query, string Tenant, int Limit, bool IncludeDrafts, global::System.Guid CorrelationId, global::System.DateTimeOffset RequestedAt, global::System.Collections.Generic.IReadOnlyList<string> Tags, global::TestApp.PreparedScope PreparedScope)",
            generatedCode);
        Assert.Contains(": this(Query, Tenant, Limit, IncludeDrafts, CorrelationId, RequestedAt, Tags)", generatedCode);
        Assert.Contains("global::System.ArgumentNullException.ThrowIfNull(PreparedScope);", generatedCode);
        Assert.Contains("this.PreparedScope = PreparedScope;", generatedCode);
        Assert.Contains("""<param name="Query">The query text.</param>""", generatedCode);
        Assert.Contains("""<param name="Tenant">The tenant identifier.</param>""", generatedCode);
        Assert.Contains("""<param name="PreparedScope">The prepared execution scope.</param>""", generatedCode);
    }

    [Fact]
    public void NullablePropertyWithoutNullRejectingGuard_RemainsNullable()
    {
        var source = """
            #nullable enable
            using NexusLabs.Needlr.Generators;

            namespace TestApp;

            public partial record Request(string Name)
            {
                [RecordConstructorOverloadParameter]
                public string? OptionalScope { get; init; }
            }
            """;

        var generatedCode = RunGenerator(source);

        Assert.Contains("string? OptionalScope", generatedCode);
        Assert.DoesNotContain("ThrowIfNull(OptionalScope)", generatedCode);
    }

    [Fact]
    public void BuiltInNotNull_RemovesOnlyTopLevelNullableAnnotation()
    {
        var source = """
            #nullable enable
            using NexusLabs.Needlr.Generators;

            namespace TestApp;

            public partial record Request(string Name)
            {
                [RecordConstructorOverloadParameter]
                [ConstructorGuard(ConstructorGuardKind.NotNull)]
                public global::System.Collections.Generic.List<string?>? Values { get; init; }
            }
            """;

        var generatedCode = RunGenerator(source);

        Assert.Contains(
            "global::System.Collections.Generic.List<string?> Values",
            generatedCode);
        Assert.DoesNotContain(
            "global::System.Collections.Generic.List<string?>? Values",
            generatedCode);
    }

    [Fact]
    public void MultipleMarkedProperties_PreserveDeclarationOrder()
    {
        var source = """
            #nullable enable
            using NexusLabs.Needlr.Generators;

            namespace TestApp;

            public partial record Request(string Name)
            {
                [RecordConstructorOverloadParameter]
                public int Second { get; init; }

                [RecordConstructorOverloadParameter]
                public string? Third { get; init; }
            }
            """;

        var generatedCode = RunGenerator(source);
        var signature = generatedCode.Split('\n').Single(line => line.Contains("public Request("));

        Assert.Contains("string Name, int Second, string? Third", signature);
    }

    [Fact]
    public void MarkedPropertiesAcrossPartialFiles_UseFilePathThenSourceOrder()
    {
        const string primarySource = """
            namespace TestApp;

            public partial record Request(string Name);
            """;
        const string laterFile = """
            using NexusLabs.Needlr.Generators;

            namespace TestApp;

            public partial record Request
            {
                [RecordConstructorOverloadParameter]
                public int Third { get; init; }
            }
            """;
        const string earlierFile = """
            using NexusLabs.Needlr.Generators;

            namespace TestApp;

            public partial record Request
            {
                [RecordConstructorOverloadParameter]
                public int Second { get; init; }
            }
            """;

        var files = GeneratorTestRunner.ForConstructorGeneration()
            .WithSourceFile("Request.cs", primarySource)
            .WithSourceFile("Z.Properties.cs", laterFile)
            .WithSourceFile("A.Properties.cs", earlierFile)
            .RunGenerator(new RecordConstructorOverloadGenerator());
        var generatedCode = Assert.Single(files).Content;
        var signature = generatedCode
            .Split('\n')
            .Single(line => line.Contains("public Request("));

        Assert.Contains("string Name, int Second, int Third", signature);
    }

    [Fact]
    public void GenericRecord_PreservesTupleEscapedIdentifiersAndParamsAsArray()
    {
        var source = """
            #nullable enable
            using NexusLabs.Needlr.Generators;

            namespace TestApp;

            public sealed partial record @event<T>(
                T Value,
                (int Left, string Right) Pair,
                params string[] @params)
                where T : class
            {
                [RecordConstructorOverloadParameter]
                public string? @class { get; init; }
            }
            """;

        var generatedCode = RunGenerator(source);
        var errors = GeneratorTestRunner.ForConstructorGeneration()
            .WithSource(source)
            .RunGeneratorCompilationErrors(new RecordConstructorOverloadGenerator());

        Assert.Contains("partial record class @event<T>", generatedCode);
        Assert.Contains(
            "T Value, (int Left, string Right) Pair, string[] @params, string? @class",
            generatedCode);
        Assert.DoesNotContain("params string[]", generatedCode);
        Assert.Contains(": this(Value, Pair, @params)", generatedCode);
        Assert.Empty(errors);
    }

    [Fact]
    public void PrimaryParameterDefaults_AreNotEmitted()
    {
        var source = """
            using NexusLabs.Needlr.Generators;

            namespace TestApp;

            public partial record Request(string Name = "fallback")
            {
                [RecordConstructorOverloadParameter]
                public int Count { get; init; }
            }
            """;

        var generatedCode = RunGenerator(source);
        var signature = generatedCode.Split('\n').Single(line => line.Contains("public Request("));

        Assert.DoesNotContain("=", signature);
        Assert.Contains("string Name, int Count", signature);
    }

    [Fact]
    public void CustomAndParameterizedAliasGuards_EmitDirectCalls()
    {
        var source = """
            #nullable enable
            using NexusLabs.Needlr.Generators;

            namespace TestApp;

            public static class ScopeGuard
            {
                public static void Validate(string? value, string parameterName) { }
            }

            public static class MinLengthGuard
            {
                public static void Validate(string? value, int minimum, string parameterName) { }
            }

            [ConstructorGuardDefinition(typeof(MinLengthGuard))]
            [global::System.AttributeUsage(global::System.AttributeTargets.Property)]
            public sealed class MinLengthAttribute : global::System.Attribute
            {
                public MinLengthAttribute(int minimum) { }
            }

            public partial record Request(string Name)
            {
                [RecordConstructorOverloadParameter]
                [ConstructorGuard(typeof(ScopeGuard))]
                public string? Scope { get; init; }

                [RecordConstructorOverloadParameter]
                [MinLength(3)]
                public string? Code { get; init; }
            }
            """;

        var generatedCode = RunGenerator(source);

        Assert.Contains("global::TestApp.ScopeGuard.Validate(Scope, nameof(Scope));", generatedCode);
        Assert.Contains("global::TestApp.MinLengthGuard.Validate(Code, 3, nameof(Code));", generatedCode);
    }

    [Fact]
    public void ReferencedAssemblyParameterizedAlias_EmitsDirectGuardCall()
    {
        const string guardAssemblySource = """
            using System;
            using NexusLabs.Needlr.Generators;

            namespace GuardLibrary;

            public static class MinLengthGuard
            {
                public static void Validate(
                    string? value,
                    int minimum,
                    string parameterName)
                {
                }
            }

            [ConstructorGuardDefinition(typeof(MinLengthGuard))]
            [AttributeUsage(AttributeTargets.Property)]
            public sealed class MinLengthAttribute : Attribute
            {
                public MinLengthAttribute(int minimum)
                {
                }
            }
            """;
        const string source = """
            #nullable enable
            using GuardLibrary;
            using NexusLabs.Needlr.Generators;

            namespace TestApp;

            public partial record Request(string Name)
            {
                [RecordConstructorOverloadParameter]
                [MinLength(3)]
                public string? Code { get; init; }
            }
            """;

        var files = GeneratorTestRunner.ForConstructorGeneration()
            .WithCrossAssemblySource("GuardLibrary", guardAssemblySource)
            .WithSource(source)
            .RunGenerator(new RecordConstructorOverloadGenerator());
        var generatedCode = Assert.Single(files).Content;

        Assert.Contains(
            "global::GuardLibrary.MinLengthGuard.Validate(Code, 3, nameof(Code));",
            generatedCode);
    }

    [Fact]
    public void BuiltInGuardsEmitExceptionDocs_CustomGuardsDoNotFabricateThem()
    {
        var source = """
            #nullable enable
            using NexusLabs.Needlr.Generators;

            namespace TestApp;

            public static class ScopeGuard
            {
                public static void Validate(string? value, string parameterName) { }
            }

            public partial record Request(string Name)
            {
                [RecordConstructorOverloadParameter]
                [ConstructorGuard(ConstructorGuardKind.NotNullOrWhiteSpace)]
                public string? Tenant { get; init; }

                [RecordConstructorOverloadParameter]
                [ConstructorGuard(typeof(ScopeGuard))]
                public string? Scope { get; init; }
            }
            """;

        var generatedCode = RunGenerator(source);

        Assert.Equal(1, CountOccurrences(generatedCode, """<exception cref="global::System.ArgumentNullException">"""));
        Assert.Equal(1, CountOccurrences(generatedCode, """<exception cref="global::System.ArgumentException">"""));
        Assert.DoesNotContain("Scope</paramref> is", generatedCode);
    }

    [Fact]
    public void CopyConstructor_RemainsUsable()
    {
        var source = """
            using NexusLabs.Needlr.Generators;

            namespace TestApp;

            public partial record Request(string Name)
            {
                [RecordConstructorOverloadParameter]
                public int Count { get; init; }

                public Request CopyViaConstructor() => new(this);
            }
            """;

        var errors = GeneratorTestRunner.ForConstructorGeneration()
            .WithSource(source)
            .RunGeneratorCompilationErrors(new RecordConstructorOverloadGenerator());

        Assert.Empty(errors);
    }

    [Fact]
    public void CopyConstructorSignatureCollision_EmitsNoOverload()
    {
        var source = """
            #nullable enable
            using NexusLabs.Needlr.Generators;

            namespace TestApp;

            public partial record Request()
            {
                [RecordConstructorOverloadParameter]
                public Request? Other { get; init; }
            }
            """;

        var generatedCode = RunGenerator(source);

        Assert.Equal(string.Empty, generatedCode);
    }

    [Fact]
    public void ExplicitConstructorSignatureCollision_EmitsNoOverload()
    {
        var source = """
            using NexusLabs.Needlr.Generators;

            namespace TestApp;

            public partial record Request(string Name)
            {
                [RecordConstructorOverloadParameter]
                public int Count { get; init; }

                public Request(string Name, int Count) : this(Name)
                {
                    this.Count = Count;
                }
            }
            """;

        var generatedCode = RunGenerator(source);

        Assert.Equal(string.Empty, generatedCode);
    }

    [Fact]
    public void GuardOnUnmarkedProperty_DoesNotTriggerOverload()
    {
        var source = """
            using NexusLabs.Needlr.Generators;

            namespace TestApp;

            public partial record Request(string Name)
            {
                [ConstructorGuard(ConstructorGuardKind.NotNull)]
                public string? Scope { get; init; }
            }
            """;

        var generatedCode = RunGenerator(source);

        Assert.Equal(string.Empty, generatedCode);
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = haystack.IndexOf(needle, index, System.StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }

    private static string RunGenerator(string source)
    {
        var files = GeneratorTestRunner.ForConstructorGeneration()
            .WithDocumentationMode()
            .WithSource(source)
            .RunGenerator(new RecordConstructorOverloadGenerator());

        return files.Length == 0
            ? string.Empty
            : string.Join("\n\n", files.Select(file => file.Content));
    }
}
