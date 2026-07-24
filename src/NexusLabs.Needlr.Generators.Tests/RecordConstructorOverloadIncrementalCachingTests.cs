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

        AssertAllOutputsCachedOrUnchanged(secondRun, RecordConstructorOverloadTrackingNames.Models);
        AssertAllOutputsCachedOrUnchanged(secondRun, RecordConstructorOverloadTrackingNames.Output);
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

        var modelReasons = GetReasons(secondRun, RecordConstructorOverloadTrackingNames.Models);
        var outputReasons = GetReasons(secondRun, RecordConstructorOverloadTrackingNames.Output);

        Assert.Equal(2, modelReasons.Length);
        Assert.Equal(1, modelReasons.Count(reason => reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged));
        Assert.Equal(1, modelReasons.Count(reason => reason is IncrementalStepRunReason.Modified or IncrementalStepRunReason.New));
        Assert.Equal(2, outputReasons.Length);
        Assert.Equal(1, outputReasons.Count(reason => reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged));
        Assert.Equal(1, outputReasons.Count(reason => reason is IncrementalStepRunReason.Modified or IncrementalStepRunReason.New));
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

        driver = (CSharpGeneratorDriver)driver.RunGenerators(before);
        driver = (CSharpGeneratorDriver)driver.RunGenerators(after);
        return driver.GetRunResult().Results.Single();
    }

    private static void AssertAllOutputsCachedOrUnchanged(GeneratorRunResult result, string trackingName)
    {
        var reasons = GetReasons(result, trackingName);
        Assert.NotEmpty(reasons);
        Assert.All(
            reasons,
            reason => Assert.True(
                reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged,
                $"Expected '{trackingName}' to be cached or unchanged, but found '{reason}'"));
    }

    private static IncrementalStepRunReason[] GetReasons(GeneratorRunResult result, string trackingName)
    {
        Assert.True(
            result.TrackedSteps.TryGetValue(trackingName, out var steps),
            $"Expected tracked step '{trackingName}'");

        return steps
            .SelectMany(step => step.Outputs)
            .Select(output => output.Reason)
            .ToArray();
    }
}
