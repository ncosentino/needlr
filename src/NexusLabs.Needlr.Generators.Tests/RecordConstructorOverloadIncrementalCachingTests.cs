using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using Xunit;

namespace NexusLabs.Needlr.Generators.Tests;

/// <summary>
/// Incremental-caching tests for <see cref="RecordConstructorOverloadGenerator"/>.
/// </summary>
public sealed class RecordConstructorOverloadIncrementalCachingTests
{
    [Fact]
    public void UnrelatedEdit_LeavesRecordModelAndOutputCached()
    {
        const string recordSource = """
            using NexusLabs.Needlr.Generators;

            namespace TestApp;

            public partial record Request(string Name)
            {
                [RecordConstructorOverloadParameter]
                public int Count { get; init; }
            }
            """;
        const string unrelatedBefore = "namespace TestApp; public static class Other { public static int Value => 1; }";
        const string unrelatedAfter = "namespace TestApp; public static class Other { public static int Value => 2; }";

        var parseOptions = new CSharpParseOptions();
        var recordTree = CSharpSyntaxTree.ParseText(
            recordSource,
            parseOptions,
            "Request.cs",
            cancellationToken: TestContext.Current.CancellationToken);
        var unrelatedTree = CSharpSyntaxTree.ParseText(
            unrelatedBefore,
            parseOptions,
            "Other.cs",
            cancellationToken: TestContext.Current.CancellationToken);
        var before = CreateCompilation(recordTree, unrelatedTree);
        var after = before.ReplaceSyntaxTree(
            unrelatedTree,
            CSharpSyntaxTree.ParseText(
                unrelatedAfter,
                parseOptions,
                "Other.cs",
                cancellationToken: TestContext.Current.CancellationToken));

        var secondRun = RunIncremental(before, after);

        IncrementalCachingAssertions.AssertAllOutputsCachedOrUnchanged(
            secondRun,
            RecordConstructorOverloadTrackingNames.Models);
        IncrementalCachingAssertions.AssertAllOutputsCachedOrUnchanged(
            secondRun,
            RecordConstructorOverloadTrackingNames.Output);
    }

    [Fact]
    public void EditingOneRecord_InvalidatesOnlyThatRecordsModelAndOutput()
    {
        const string beforeSource = """
            using NexusLabs.Needlr.Generators;

            namespace TestApp;

            public partial record First(string Name)
            {
                [RecordConstructorOverloadParameter]
                public int Count { get; init; }
            }

            public partial record Second(string Name)
            {
                [RecordConstructorOverloadParameter]
                public int Count { get; init; }
            }
            """;
        const string afterSource = """
            using NexusLabs.Needlr.Generators;

            namespace TestApp;

            public partial record First(string Name)
            {
                [RecordConstructorOverloadParameter]
                public int Count { get; init; }
            }

            public partial record Second(string Name)
            {
                [RecordConstructorOverloadParameter]
                public long Count { get; init; }
            }
            """;

        var parseOptions = new CSharpParseOptions();
        var beforeTree = CSharpSyntaxTree.ParseText(
            beforeSource,
            parseOptions,
            "Records.cs",
            cancellationToken: TestContext.Current.CancellationToken);
        var before = CreateCompilation(beforeTree);
        var after = before.ReplaceSyntaxTree(
            beforeTree,
            CSharpSyntaxTree.ParseText(
                afterSource,
                parseOptions,
                "Records.cs",
                cancellationToken: TestContext.Current.CancellationToken));

        var secondRun = RunIncremental(before, after);

        IncrementalCachingAssertions.AssertExactlyOneChangedAndOneCached(
            secondRun,
            RecordConstructorOverloadTrackingNames.Models);
        IncrementalCachingAssertions.AssertExactlyOneChangedAndOneCached(
            secondRun,
            RecordConstructorOverloadTrackingNames.Output);
    }

    [Theory]
    [InlineData(
        "string Name",
        "string Title",
        "[RecordConstructorOverloadParameter]\npublic int Count { get; init; }",
        "[RecordConstructorOverloadParameter]\npublic int Count { get; init; }")]
    [InlineData(
        "string Name",
        "object Name",
        "[RecordConstructorOverloadParameter]\npublic int Count { get; init; }",
        "[RecordConstructorOverloadParameter]\npublic int Count { get; init; }")]
    [InlineData(
        "string Name, int Age",
        "int Age, string Name",
        "[RecordConstructorOverloadParameter]\npublic int Count { get; init; }",
        "[RecordConstructorOverloadParameter]\npublic int Count { get; init; }")]
    [InlineData(
        "string Name",
        "string? Name",
        "[RecordConstructorOverloadParameter]\npublic int Count { get; init; }",
        "[RecordConstructorOverloadParameter]\npublic int Count { get; init; }")]
    [InlineData(
        "string Name",
        "string Name",
        "[RecordConstructorOverloadParameter]\npublic int Count { get; init; }",
        "[RecordConstructorOverloadParameter]\npublic int Total { get; init; }")]
    [InlineData(
        "string Name",
        "string Name",
        "[RecordConstructorOverloadParameter]\npublic int Count { get; init; }",
        "[RecordConstructorOverloadParameter]\npublic long Count { get; init; }")]
    [InlineData(
        "string Name",
        "string Name",
        "[RecordConstructorOverloadParameter]\npublic int Count { get; init; }\n[RecordConstructorOverloadParameter]\npublic bool Enabled { get; init; }",
        "[RecordConstructorOverloadParameter]\npublic bool Enabled { get; init; }\n[RecordConstructorOverloadParameter]\npublic int Count { get; init; }")]
    [InlineData(
        "string Name",
        "string Name",
        "[RecordConstructorOverloadParameter]\n[ConstructorGuard(ConstructorGuardKind.NotNull)]\npublic string? Label { get; init; }",
        "[RecordConstructorOverloadParameter]\n[ConstructorGuard(ConstructorGuardKind.NotNullOrEmpty)]\npublic string? Label { get; init; }")]
    [InlineData(
        "string Name",
        "string Name",
        "[RecordConstructorOverloadParameter]\n[ConstructorGuard(typeof(CustomGuard), nameof(CustomGuard.Validate))]\npublic string Label { get; init; } = \"\";",
        "[RecordConstructorOverloadParameter]\n[ConstructorGuard(typeof(CustomGuard), nameof(CustomGuard.Check))]\npublic string Label { get; init; } = \"\";")]
    [InlineData(
        "string Name",
        "string Name",
        "[RecordConstructorOverloadParameter]\n[MinLength(3)]\npublic string Label { get; init; } = \"\";",
        "[RecordConstructorOverloadParameter]\n[MinLength(5)]\npublic string Label { get; init; } = \"\";")]
    public void SemanticRecordEdit_InvalidatesOnlyAffectedRecordsModelAndOutput(
        string beforePrimaryParameters,
        string afterPrimaryParameters,
        string beforeProperties,
        string afterProperties)
    {
        var beforeSource = CreateSemanticEditSource(beforePrimaryParameters, beforeProperties);
        var afterSource = CreateSemanticEditSource(afterPrimaryParameters, afterProperties);
        var parseOptions = new CSharpParseOptions();
        var beforeTree = CSharpSyntaxTree.ParseText(
            beforeSource,
            parseOptions,
            "Records.cs",
            cancellationToken: TestContext.Current.CancellationToken);
        var beforeCompilation = CreateCompilation(beforeTree);
        var afterCompilation = beforeCompilation.ReplaceSyntaxTree(
            beforeTree,
            CSharpSyntaxTree.ParseText(
                afterSource,
                parseOptions,
                "Records.cs",
                cancellationToken: TestContext.Current.CancellationToken));

        var secondRun = RunIncremental(beforeCompilation, afterCompilation);

        IncrementalCachingAssertions.AssertExactlyOneChangedAndOneCached(
            secondRun,
            RecordConstructorOverloadTrackingNames.Models);
        IncrementalCachingAssertions.AssertExactlyOneChangedAndOneCached(
            secondRun,
            RecordConstructorOverloadTrackingNames.Output);
    }

    private static CSharpCompilation CreateCompilation(params SyntaxTree[] syntaxTrees)
    {
        return CSharpCompilation.Create(
            "RecordConstructorOverloadCaching",
            syntaxTrees,
            Basic.Reference.Assemblies.Net100.References.All
                .Concat(new[] { MetadataReference.CreateFromFile(typeof(GenerateConstructorAttribute).Assembly.Location) }),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static GeneratorRunResult RunIncremental(CSharpCompilation before, CSharpCompilation after)
    {
        var driver = CSharpGeneratorDriver.Create(
            generators: [new RecordConstructorOverloadGenerator().AsSourceGenerator()],
            driverOptions: new GeneratorDriverOptions(
                disabledOutputs: IncrementalGeneratorOutputKind.None,
                trackIncrementalGeneratorSteps: true));

        driver = (CSharpGeneratorDriver)driver.RunGenerators(
            before,
            TestContext.Current.CancellationToken);
        driver = (CSharpGeneratorDriver)driver.RunGenerators(
            after,
            TestContext.Current.CancellationToken);
        return driver.GetRunResult().Results.Single();
    }

    private static string CreateSemanticEditSource(
        string primaryParameters,
        string properties)
    {
        const string template = """
            using System;
            using NexusLabs.Needlr.Generators;

            namespace TestApp;

            public static class CustomGuard
            {
                public static void Validate(string value, string parameterName) { }
                public static void Check(string value, string parameterName) { }
            }

            public static class MinLengthGuard
            {
                public static void Validate(string value, int minimum, string parameterName) { }
            }

            [ConstructorGuardDefinition(typeof(MinLengthGuard))]
            [AttributeUsage(AttributeTargets.Property)]
            public sealed class MinLengthAttribute : Attribute
            {
                public MinLengthAttribute(int minimum) { }
            }

            public partial record First(PRIMARY_PARAMETERS)
            {
            PROPERTIES
            }

            public partial record Second(string Name)
            {
                [RecordConstructorOverloadParameter]
                public int Count { get; init; }
            }
            """;

        const string indentation = "    ";
        return template
            .Replace("PRIMARY_PARAMETERS", primaryParameters, StringComparison.Ordinal)
            .Replace(
                "PROPERTIES",
                indentation + properties.Replace("\n", "\n" + indentation, StringComparison.Ordinal),
                StringComparison.Ordinal);
    }

}
