using System.Text.RegularExpressions;

using NexusLabs.Needlr.Generators.Export;
using NexusLabs.Needlr.Generators.Tests.Diagnostics;

using Xunit;

namespace NexusLabs.Needlr.Generators.Tests;

/// <summary>
/// Guards the determinism contract for generated source. Anything handed to
/// <c>AddSource</c> is compiled into the consumer's assembly, so a wall-clock value
/// there defeats <c>&lt;Deterministic&gt;true&lt;/Deterministic&gt;</c> and makes the
/// assembly hash change between builds of identical inputs.
/// </summary>
/// <remarks>
/// These assertions are structural rather than comparative on purpose. Running the
/// generator twice and diffing the text would not catch a clock read: the timestamps
/// previously emitted had one-second resolution, so two in-process runs almost always
/// produced identical text and the test would pass while the defect was present.
/// </remarks>
public sealed class GeneratedSourceDeterminismTests
{
    private const string TimestampPattern =
        @"\d{4}-\d{2}-\d{2}[ T]\d{2}:\d{2}:\d{2}";

    private const string Source = """
        using NexusLabs.Needlr.Generators;

        [assembly: GenerateTypeRegistry]

        namespace TestApp
        {
            public interface IThing { }

            public sealed class Thing : IThing { }
        }
        """;

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void GeneratedSource_ContainsNoWallClockTimestamp(
        bool diagnosticsEnabled,
        bool graphEnabled)
    {
        var generated = GeneratorTestRunner.ForTypeRegistry()
            .WithSource(Source)
            .WithDiagnosticsEnabled(diagnosticsEnabled)
            .WithGraphExportEnabled(graphEnabled)
            .RunTypeRegistryGeneratorFiles();

        Assert.NotEmpty(generated);

        foreach (var file in generated)
        {
            var withoutKnownConstants = file.Content
                .Replace(GraphExporter.GeneratedAtSentinel, string.Empty)
                .Replace(DiagnosticsGenerator.GeneratedAtPlaceholder, string.Empty);

            Assert.False(
                Regex.IsMatch(withoutKnownConstants, TimestampPattern),
                $"'{file.FilePath}' contains a wall-clock timestamp. Generated source must be "
                    + "byte-identical across builds; stamp the time where the artifact is "
                    + "written instead.");
        }
    }

    [Fact]
    public void DiagnosticsSource_EmitsPlaceholderForLaterSubstitution()
    {
        var generated = GeneratorTestRunner.ForTypeRegistry()
            .WithSource(Source)
            .WithDiagnosticsEnabled()
            .RunTypeRegistryGeneratorFiles();

        var diagnostics = Assert.Single(
            generated,
            f => f.FilePath.EndsWith("NeedlrDiagnostics.g.cs", StringComparison.Ordinal));

        Assert.Contains(DiagnosticsGenerator.GeneratedAtPlaceholder, diagnostics.Content);
    }

    [Fact]
    public void GraphSource_EmitsSentinelForLaterSubstitution()
    {
        var generated = GeneratorTestRunner.ForTypeRegistry()
            .WithSource(Source)
            .WithGraphExportEnabled()
            .RunTypeRegistryGeneratorFiles();

        var graph = Assert.Single(
            generated,
            f => f.FilePath.EndsWith("NeedlrGraph.g.cs", StringComparison.Ordinal));

        Assert.Contains(GraphExporter.GeneratedAtSentinel, graph.Content);
        Assert.Contains("WriteGraphToFile", graph.Content);
    }
}
