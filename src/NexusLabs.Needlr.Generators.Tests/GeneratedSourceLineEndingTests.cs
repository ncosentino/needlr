using NexusLabs.Needlr;
using NexusLabs.Needlr.Generators.Tests.Diagnostics;

using Xunit;

namespace NexusLabs.Needlr.Generators.Tests;

/// <summary>
/// Guards generated source against the host operating system's line ending.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="System.Text.StringBuilder.AppendLine()"/> emits
/// <see cref="System.Environment.NewLine"/>, which is CRLF on Windows and LF elsewhere.
/// Generated source therefore differed byte-for-byte between a Windows build and a Linux
/// build of identical input, which is the last cross-platform gap in generator output.
/// </para>
/// <para>
/// Fixing this at the roughly 1,272 <c>AppendLine</c> call sites is not viable. Emitted
/// text is normalized once at the <c>AddSource</c> boundary instead, so this test asserts
/// the invariant at that same boundary: no generated file may contain a carriage return.
/// </para>
/// <para>
/// The assertion is deliberately "no CR at all" rather than a Windows-versus-Linux
/// comparison. It is a single invariant that holds on every platform, so it stays
/// meaningful on a Linux agent where the defect could never have reproduced.
/// </para>
/// </remarks>
public sealed class GeneratedSourceLineEndingTests
{
    private const string Source = """
        using NexusLabs.Needlr;
        using NexusLabs.Needlr.Generators;

        [assembly: GenerateTypeRegistry]

        namespace TestApp
        {
            public interface IThing { }

            public sealed class Thing : IThing { }

            [DecoratorFor<IThing>(Order = 1)]
            public sealed class ThingDecorator : IThing
            {
                public ThingDecorator(IThing inner) { }
            }
        }
        """;

    [Fact]
    public void GeneratedFiles_ContainNoCarriageReturn()
    {
        foreach (var file in Generate())
        {
            Assert.False(
                file.Content.IndexOf('\r') >= 0,
                $"'{file.FilePath}' contains a carriage return. Emitted text is normalized to "
                    + "LF at the AddSource boundary so output does not depend on the host OS.");
        }
    }

    [Fact]
    public void GeneratedFiles_AreNonEmpty()
    {
        var generated = Generate();

        Assert.NotEmpty(generated);
        Assert.All(generated, f => Assert.False(string.IsNullOrWhiteSpace(f.Content)));
    }

    private static GeneratedFile[] Generate()
    {
        return GeneratorTestRunner.ForTypeRegistry()
            .WithReference<DecoratorForAttribute<object>>()
            .WithSource(Source)
            .WithDiagnosticsEnabled()
            .WithGraphExportEnabled()
            .RunTypeRegistryGeneratorFiles();
    }
}
