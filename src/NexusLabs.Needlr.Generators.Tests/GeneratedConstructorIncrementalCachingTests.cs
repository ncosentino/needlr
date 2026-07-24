using System;
using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

using Xunit;

namespace NexusLabs.Needlr.Generators.Tests;

/// <summary>
/// Proves, using Roslyn's tracked-incremental-step APIs
/// (<see cref="GeneratorDriverOptions.TrackIncrementalGeneratorSteps"/>,
/// <see cref="GeneratorRunResult.TrackedSteps"/>, <see cref="IncrementalStepRunReason"/>),
/// that <see cref="GeneratedConstructorGenerator"/>'s pipeline invalidates only the
/// constructor-generation inputs actually affected by an edit: unrelated edits leave
/// every model and generated file cached, and editing one type's eligible field
/// recomputes only that type.
/// </summary>
public sealed class GeneratedConstructorIncrementalCachingTests
{
    [Fact]
    public void UnrelatedSourceEdit_LeavesGeneratedConstructorModelAndOutputCached()
    {
        const string typeSource = """
            using NexusLabs.Needlr.Generators;

            namespace TestApp
            {
                public interface IRepository { }

                [GenerateConstructor]
                public partial class UserService
                {
                    private readonly IRepository _repository;
                }
            }
            """;

        const string unrelatedBefore = """
            namespace TestApp
            {
                public class Unrelated
                {
                    public int Value => 1;
                }
            }
            """;

        const string unrelatedAfter = """
            namespace TestApp
            {
                public class Unrelated
                {
                    public int Value => 2;
                }
            }
            """;

        var (before, unrelatedTree) = CreateCompilationWithTrackedTree(
            "Unrelated.cs",
            ("UserService.cs", typeSource),
            ("Unrelated.cs", unrelatedBefore));

        var after = before.ReplaceSyntaxTree(unrelatedTree, EditTree(unrelatedTree, unrelatedBefore, unrelatedAfter));

        var (_, secondRun) = RunIncremental(before, after);

        AssertAllOutputsCachedOrUnchanged(secondRun, GeneratedConstructorTrackingNames.Models);
        AssertAllOutputsCachedOrUnchanged(secondRun, GeneratedConstructorTrackingNames.Output);
    }

    [Fact]
    public void EditingOneEligibleField_InvalidatesOnlyThatTypesModelAndOutput()
    {
        const string before = """
            using NexusLabs.Needlr.Generators;

            namespace TestApp
            {
                public interface IRepository { }
                public interface ILogger { }

                [GenerateConstructor]
                public partial class TypeA
                {
                    private readonly IRepository _repository;
                }

                [GenerateConstructor]
                public partial class TypeB
                {
                    private readonly ILogger _logger;
                }
            }
            """;

        const string after = """
            using NexusLabs.Needlr.Generators;

            namespace TestApp
            {
                public interface IRepository { }
                public interface ILogger { }

                [GenerateConstructor]
                public partial class TypeA
                {
                    private readonly IRepository _repository;
                }

                [GenerateConstructor]
                public partial class TypeB
                {
                    private readonly ILogger _auditLogger;
                }
            }
            """;

        var (beforeCompilation, tree) = CreateCompilationWithTrackedTree(
            "Types.cs",
            ("Types.cs", before));

        var afterCompilation = beforeCompilation.ReplaceSyntaxTree(tree, EditTree(tree, before, after));

        var (_, secondRun) = RunIncremental(beforeCompilation, afterCompilation);

        var modelOutputs = GetTrackedOutputs(secondRun, GeneratedConstructorTrackingNames.Models);
        var emissionOutputs = GetTrackedOutputs(secondRun, GeneratedConstructorTrackingNames.Output);

        Assert.Equal(2, modelOutputs.Length);
        Assert.Contains(modelOutputs, o => o.Reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged);
        Assert.Contains(modelOutputs, o => o.Reason is IncrementalStepRunReason.Modified or IncrementalStepRunReason.New);

        Assert.Equal(2, emissionOutputs.Length);
        Assert.Contains(emissionOutputs, o => o.Reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged);
        Assert.Contains(emissionOutputs, o => o.Reason is IncrementalStepRunReason.Modified or IncrementalStepRunReason.New);
    }

    [Fact]
    public void EditingOneAliasGuardsForwardedLiteral_InvalidatesOnlyThatTypesModelAndOutput()
    {
        // Regression test for ConstructorGuardModel's forwarded-argument-literal
        // equality: changing only the literal argument of a parameterized alias
        // attribute usage (e.g. [MinCount(3)] -> [MinCount(5)]) must be observed as a
        // real change to TypeA's model/output, while TypeB -- an entirely unrelated
        // type in the same compilation -- stays cached.
        const string before = """
            using System;
            using NexusLabs.Needlr.Generators;

            namespace TestApp
            {
                public static class MinCountGuard
                {
                    public static void Validate(int value, int min, string parameterName) { }
                }

                [ConstructorGuardDefinition(typeof(MinCountGuard))]
                [AttributeUsage(AttributeTargets.Field)]
                public sealed class MinCountAttribute : Attribute
                {
                    public MinCountAttribute(int min) { }
                }

                public interface ILogger { }

                public partial class TypeA
                {
                    [MinCount(3)]
                    private readonly int _value;
                }

                [GenerateConstructor]
                public partial class TypeB
                {
                    private readonly ILogger _logger;
                }
            }
            """;

        const string after = """
            using System;
            using NexusLabs.Needlr.Generators;

            namespace TestApp
            {
                public static class MinCountGuard
                {
                    public static void Validate(int value, int min, string parameterName) { }
                }

                [ConstructorGuardDefinition(typeof(MinCountGuard))]
                [AttributeUsage(AttributeTargets.Field)]
                public sealed class MinCountAttribute : Attribute
                {
                    public MinCountAttribute(int min) { }
                }

                public interface ILogger { }

                public partial class TypeA
                {
                    [MinCount(5)]
                    private readonly int _value;
                }

                [GenerateConstructor]
                public partial class TypeB
                {
                    private readonly ILogger _logger;
                }
            }
            """;

        var (beforeCompilation, tree) = CreateCompilationWithTrackedTree(
            "Types.cs",
            ("Types.cs", before));

        var afterCompilation = beforeCompilation.ReplaceSyntaxTree(tree, EditTree(tree, before, after));

        var (_, secondRun) = RunIncremental(beforeCompilation, afterCompilation);

        var modelOutputs = GetTrackedOutputs(secondRun, GeneratedConstructorTrackingNames.Models);
        var emissionOutputs = GetTrackedOutputs(secondRun, GeneratedConstructorTrackingNames.Output);

        Assert.Equal(2, modelOutputs.Length);
        Assert.Contains(modelOutputs, o => o.Reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged);
        Assert.Contains(modelOutputs, o => o.Reason is IncrementalStepRunReason.Modified or IncrementalStepRunReason.New);

        Assert.Equal(2, emissionOutputs.Length);
        Assert.Contains(emissionOutputs, o => o.Reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged);
        Assert.Contains(emissionOutputs, o => o.Reason is IncrementalStepRunReason.Modified or IncrementalStepRunReason.New);

        var secondRunResult = secondRun.Results.Single();
        var generatedContent = string.Join("\n\n", secondRunResult.GeneratedSources.Select(s => s.SourceText.ToString()));
        Assert.Contains("global::TestApp.MinCountGuard.Validate(value, 5, nameof(value));", generatedContent);
    }

    [Fact]
    public void MultiPartialDeclaration_ProducesExactlyOneModelAndOutput()
    {
        const string sourceA = """
            using NexusLabs.Needlr.Generators;

            namespace TestApp
            {
                public interface IRepository { }

                [GenerateConstructor]
                public partial class SplitService
                {
                    private readonly IRepository _repository;
                }
            }
            """;

        const string sourceB = """
            namespace TestApp
            {
                public partial class SplitService
                {
                    private readonly string _label = "default";
                }
            }
            """;

        var compilation = CreateCompilation(("A.cs", sourceA), ("B.cs", sourceB));

        var driver = CreateTrackingDriver();
        driver = driver.RunGenerators(compilation, TestContext.Current.CancellationToken);
        var runResult = driver.GetRunResult().Results.Single();

        var modelOutputs = runResult.TrackedSteps[GeneratedConstructorTrackingNames.Models]
            .SelectMany(s => s.Outputs)
            .ToArray();
        var emissionOutputs = runResult.TrackedSteps[GeneratedConstructorTrackingNames.Output]
            .SelectMany(s => s.Outputs)
            .ToArray();

        // Roslyn does not surface null transform results as tracked outputs, so the
        // filtered model and output steps provide the stable deduplication assertion.
        Assert.Single(modelOutputs);
        Assert.Single(emissionOutputs);
        Assert.Single(runResult.GeneratedSources);
    }

    private static (GeneratorDriverRunResult First, GeneratorDriverRunResult Second) RunIncremental(
        CSharpCompilation before,
        CSharpCompilation after)
    {
        var driver = CreateTrackingDriver();

        driver = driver.RunGenerators(before, TestContext.Current.CancellationToken);
        var firstResult = driver.GetRunResult();

        driver = driver.RunGenerators(after, TestContext.Current.CancellationToken);
        var secondResult = driver.GetRunResult();

        return (firstResult, secondResult);
    }

    private static GeneratorDriver CreateTrackingDriver()
    {
        var generator = new GeneratedConstructorGenerator();
        var driverOptions = new GeneratorDriverOptions(
            disabledOutputs: IncrementalGeneratorOutputKind.None,
            trackIncrementalGeneratorSteps: true);

        return CSharpGeneratorDriver.Create(
            generators: ImmutableArray.Create(generator.AsSourceGenerator()),
            additionalTexts: ImmutableArray<AdditionalText>.Empty,
            parseOptions: new CSharpParseOptions(),
            optionsProvider: null,
            driverOptions: driverOptions);
    }

    private static void AssertAllOutputsCachedOrUnchanged(GeneratorDriverRunResult result, string trackingName)
    {
        var outputs = GetTrackedOutputs(result, trackingName);

        Assert.NotEmpty(outputs);
        Assert.All(outputs, o => Assert.True(
            o.Reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged,
            $"Expected '{trackingName}' output to be Cached or Unchanged but was {o.Reason}."));
    }

    private static (object Value, IncrementalStepRunReason Reason)[] GetTrackedOutputs(GeneratorDriverRunResult result, string trackingName)
    {
        return result.Results.Single().TrackedSteps[trackingName]
            .SelectMany(step => step.Outputs)
            .ToArray();
    }

    private static (CSharpCompilation Compilation, SyntaxTree TrackedTree) CreateCompilationWithTrackedTree(
        string trackedFilePath,
        params (string Path, string Source)[] files)
    {
        var compilation = CreateCompilation(files);
        var trackedTree = compilation.SyntaxTrees.Single(t => t.FilePath == trackedFilePath);
        return (compilation, trackedTree);
    }

    private static CSharpCompilation CreateCompilation(params (string Path, string Source)[] files)
    {
        var parseOptions = new CSharpParseOptions();
        var trees = files
            .Select(f => CSharpSyntaxTree.ParseText(f.Source, parseOptions, path: f.Path))
            .ToArray();

        var references = Basic.Reference.Assemblies.Net100.References.All
            .Concat(new[] { MetadataReference.CreateFromFile(typeof(GenerateConstructorAttribute).Assembly.Location) })
            .ToArray();

        return CSharpCompilation.Create(
            "IncrementalCachingTestAssembly",
            trees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    /// <summary>
    /// Applies a minimal, single-range text edit from <paramref name="before"/> to
    /// <paramref name="after"/> via <see cref="SyntaxTree.WithChangedText"/> rather than
    /// a full re-parse, so Roslyn's incremental parser can reuse unaffected parts of
    /// the tree. This is the scenario the
    /// incremental generator pipeline is actually designed to cache across.
    /// </summary>
    private static SyntaxTree EditTree(SyntaxTree tree, string before, string after)
    {
        var prefixLength = 0;
        var minLength = Math.Min(before.Length, after.Length);
        while (prefixLength < minLength && before[prefixLength] == after[prefixLength])
        {
            prefixLength++;
        }

        var beforeSuffixLength = 0;
        while (beforeSuffixLength < minLength - prefixLength &&
            before[before.Length - 1 - beforeSuffixLength] == after[after.Length - 1 - beforeSuffixLength])
        {
            beforeSuffixLength++;
        }

        var changeStart = prefixLength;
        var changeOldLength = before.Length - beforeSuffixLength - prefixLength;
        var replacement = after.Substring(prefixLength, after.Length - beforeSuffixLength - prefixLength);

        var newText = tree.GetText().WithChanges(new TextChange(new TextSpan(changeStart, changeOldLength), replacement));
        return tree.WithChangedText(newText);
    }
}
