using NexusLabs.Needlr;

using Xunit;

namespace NexusLabs.Needlr.Generators.Tests;

/// <summary>
/// Guards emitted source-file paths against the host operating system.
/// </summary>
/// <remarks>
/// <para>
/// Roslyn reports <c>SyntaxTree.FilePath</c> using the host's native separator, so an
/// emitter that copies it through unchanged writes <c>Services\Thing.cs</c> on Windows
/// and <c>Services/Thing.cs</c> on Linux. Those are different bytes in the compiled
/// assembly, so the same source produces different output per operating system.
/// </para>
/// <para>
/// An absolute path is worse than a separator difference: it embeds the build machine's
/// directory layout into the shipped assembly.
/// </para>
/// </remarks>
public sealed class GeneratedSourcePathNormalizationTests
{
    private const string ProjectDirectory = @"C:\build\TestApp";

    [Fact]
    public void EmittedPaths_UseForwardSlashes_WhenHostUsesBackslashes()
    {
        var generated = Generate();

        foreach (var file in generated)
        {
            Assert.False(
                ContainsWindowsStyleSourcePath(file.Content),
                $"'{file.FilePath}' emits a backslash-separated source path. Emitted paths "
                    + "must be normalized to '/' so output does not depend on the host OS.");
        }
    }

    [Fact]
    public void EmittedPaths_AreRelative_AndNeverLeakTheBuildDirectory()
    {
        var generated = Generate();

        foreach (var file in generated)
        {
            Assert.DoesNotContain("C:/build", file.Content, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(@"C:\\build", file.Content, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void ServiceCatalog_EmitsTheSameRelativePathAsTheTypeRegistry()
    {
        var generated = Generate();

        var catalog = generated.Single(
            f => f.FilePath.EndsWith("ServiceCatalog.g.cs", StringComparison.Ordinal));

        Assert.Contains("Services/Thing.cs", catalog.Content, StringComparison.Ordinal);
        Assert.Contains("Contracts/IThing.cs", catalog.Content, StringComparison.Ordinal);
    }

    private static bool ContainsWindowsStyleSourcePath(string content)
    {
        // The emitted C# escapes a literal backslash, so a native Windows path appears
        // as "Services\\Thing.cs" in the generated text.
        return content.Contains(@"\\Thing.cs")
            || content.Contains(@"\\IThing.cs")
            || content.Contains(@"\Thing.cs")
            || content.Contains(@"\IThing.cs");
    }

    private static IReadOnlyList<GeneratedFileSnapshot> Generate()
    {
        var files = GeneratorTestRunner.ForTypeRegistry()
            .WithReference<DecoratorForAttribute<object>>()
            .WithProjectDir(ProjectDirectory)
            .WithSourceFile(
                @"C:\build\TestApp\AssemblyInfo.cs",
                """
                using NexusLabs.Needlr.Generators;

                [assembly: GenerateTypeRegistry]
                """)
            .WithSourceFile(
                @"C:\build\TestApp\Contracts\IThing.cs",
                """
                namespace TestApp
                {
                    public interface IThing { }
                }
                """)
            .WithSourceFile(
                @"C:\build\TestApp\Services\Thing.cs",
                """
                namespace TestApp
                {
                    public sealed class Thing : IThing { }
                }
                """)
            .RunTypeRegistryGeneratorFiles();

        return files
            .Select(f => new GeneratedFileSnapshot(f.FilePath, f.Content))
            .ToList();
    }

    private sealed record GeneratedFileSnapshot(string FilePath, string Content);
}
