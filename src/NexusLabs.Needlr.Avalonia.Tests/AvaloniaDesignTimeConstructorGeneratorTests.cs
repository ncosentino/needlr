using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

using Xunit;

namespace NexusLabs.Needlr.Avalonia.Tests;

public sealed class AvaloniaDesignTimeConstructorGeneratorTests
{
    // -------------------------------------------------------------------------
    // Happy path
    // -------------------------------------------------------------------------

    [Fact]
    public void HappyPath_PartialClassWithDiCtor_GeneratesDesignTimeCtor()
    {
        var output = RunGenerator(HappyPathSource);

        Assert.Contains("public MainWindow()", output);
        Assert.Contains("InitializeComponent()", output);
    }

    [Fact]
    public void HappyPath_ContainsDesignModeGuard()
    {
        var output = RunGenerator(HappyPathSource);

        Assert.Contains("Design.IsDesignMode", output);
        Assert.Contains("InvalidOperationException", output);
    }

    [Fact]
    public void HappyPath_ContainsPragmaSuppression()
    {
        var output = RunGenerator(HappyPathSource);

        Assert.Contains("#pragma warning disable CS8618", output);
        Assert.Contains("#pragma warning restore CS8618", output);
    }

    [Fact]
    public void HappyPath_UsesCorrectNamespace()
    {
        var output = RunGenerator(HappyPathSource);

        Assert.Contains("namespace TestApp;", output);
    }

    [Fact]
    public void HappyPath_EmitsPartialClass()
    {
        var output = RunGenerator(HappyPathSource);

        Assert.Contains("partial class MainWindow", output);
    }

    // -------------------------------------------------------------------------
    // Diagnostics
    // -------------------------------------------------------------------------

    [Fact]
    public void NonPartialClass_EmitsNDLRAVA001()
    {
        var diagnostics = RunGeneratorDiagnostics(NonPartialSource);

        var error = diagnostics.FirstOrDefault(d => d.Id == "NDLRAVA001");
        Assert.NotNull(error);
        Assert.Equal(DiagnosticSeverity.Error, error.Severity);
        Assert.Contains("partial", error.GetMessage());
    }

    [Fact]
    public void AlreadyHasParameterlessCtor_EmitsNDLRAVA002()
    {
        var diagnostics = RunGeneratorDiagnostics(AlreadyHasParameterlessCtorSource);

        var error = diagnostics.FirstOrDefault(d => d.Id == "NDLRAVA002");
        Assert.NotNull(error);
        Assert.Equal(DiagnosticSeverity.Error, error.Severity);
    }

    [Fact]
    public void NoParameterizedCtor_EmitsNDLRAVA003()
    {
        var diagnostics = RunGeneratorDiagnostics(NoParameterizedCtorSource);

        var warning = diagnostics.FirstOrDefault(d => d.Id == "NDLRAVA003");
        Assert.NotNull(warning);
        Assert.Equal(DiagnosticSeverity.Warning, warning.Severity);
    }

    [Fact]
    public void NonPartialClass_DoesNotGenerateCode()
    {
        var output = RunGenerator(NonPartialSource);

        Assert.DoesNotContain("InitializeComponent", output);
    }

    // -------------------------------------------------------------------------
    // Test sources
    // -------------------------------------------------------------------------

    private const string AttributeSource = @"
namespace NexusLabs.Needlr.Avalonia
{
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public sealed class GenerateAvaloniaDesignTimeConstructorAttribute : System.Attribute { }
}

namespace Avalonia.Controls
{
    public static class Design
    {
        public static bool IsDesignMode => false;
    }
}
";

    private const string HappyPathSource = @"
namespace TestApp
{
    public class ViewModel { }

    [NexusLabs.Needlr.Avalonia.GenerateAvaloniaDesignTimeConstructor]
    public partial class MainWindow
    {
        public MainWindow(ViewModel vm) { InitializeComponent(); }
        private void InitializeComponent() { }
    }
}
";

    private const string NonPartialSource = @"
namespace TestApp
{
    public class ViewModel { }

    [NexusLabs.Needlr.Avalonia.GenerateAvaloniaDesignTimeConstructor]
    public class NotPartialWindow
    {
        public NotPartialWindow(ViewModel vm) { }
    }
}
";

    private const string AlreadyHasParameterlessCtorSource = @"
namespace TestApp
{
    public class ViewModel { }

    [NexusLabs.Needlr.Avalonia.GenerateAvaloniaDesignTimeConstructor]
    public partial class WindowWithBothCtors
    {
        public WindowWithBothCtors() { }
        public WindowWithBothCtors(ViewModel vm) { }
    }
}
";

    private const string NoParameterizedCtorSource = @"
namespace TestApp
{
    [NexusLabs.Needlr.Avalonia.GenerateAvaloniaDesignTimeConstructor]
    public partial class WindowWithNoCtor
    {
    }
}
";

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static string RunGenerator(string source)
    {
        var (_, generatedTrees) = RunGeneratorCore(source);
        return string.Join("\n\n", generatedTrees
            .Where(t => t.FilePath.Contains("DesignTimeCtor"))
            .Select(t => t.GetText().ToString()));
    }

    private static ImmutableArray<Diagnostic> RunGeneratorDiagnostics(string source)
    {
        var (diagnostics, _) = RunGeneratorCore(source);
        return diagnostics;
    }

    private static (ImmutableArray<Diagnostic> diagnostics, ImmutableArray<SyntaxTree> generatedTrees) RunGeneratorCore(string source)
    {
        var combinedSource = AttributeSource + "\n" + source;

        var syntaxTree = CSharpSyntaxTree.ParseText(combinedSource);
        var references = Basic.Reference.Assemblies.Net100.References.All;

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new AvaloniaDesignTimeConstructorGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var diagnostics);

        var generatedTrees = outputCompilation.SyntaxTrees
            .Where(t => t.FilePath.EndsWith(".g.cs"))
            .ToImmutableArray();

        return (diagnostics, generatedTrees);
    }
}
