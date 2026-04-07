using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using Xunit;

namespace NexusLabs.Needlr.AgentFramework.Generators.Tests;

public class AsyncLocalScopedGeneratorTests
{
    private const string AttributeStub = """
        namespace NexusLabs.Needlr.AgentFramework
        {
            [System.AttributeUsage(System.AttributeTargets.Interface, AllowMultiple = false)]
            public sealed class AsyncLocalScopedAttribute : System.Attribute
            {
                public bool Mutable { get; set; }
            }
        }
        """;

    // -------------------------------------------------------------------------
    // Simple variant
    // -------------------------------------------------------------------------

    [Fact]
    public void SimpleVariant_GeneratesClassImplementingInterface()
    {
        var source = """
            using NexusLabs.Needlr.AgentFramework;
            namespace TestApp
            {
                [AsyncLocalScoped]
                public interface IMyAccessor
                {
                    string? Current { get; }
                    System.IDisposable BeginScope(string value);
                }
            }
            """;

        var generated = RunGenerator(source);

        Assert.Contains("internal sealed class MyAccessor", generated);
        Assert.Contains(": global::TestApp.IMyAccessor", generated);
        Assert.Contains("AsyncLocal<string?>", generated);
        Assert.Contains("public string? Current => _current.Value", generated);
        Assert.Contains("BeginScope(string value)", generated);
    }

    // -------------------------------------------------------------------------
    // Mutable variant
    // -------------------------------------------------------------------------

    [Fact]
    public void MutableVariant_GeneratesHolderPattern()
    {
        var source = """
            using NexusLabs.Needlr.AgentFramework;
            namespace TestApp
            {
                [AsyncLocalScoped(Mutable = true)]
                public interface IMutableAccessor
                {
                    int? Current { get; }
                    System.IDisposable BeginCapture();
                }
            }
            """;

        var generated = RunGenerator(source);

        Assert.Contains("internal sealed class MutableAccessor", generated);
        Assert.Contains("AsyncLocal<Holder?>", generated);
        Assert.Contains("Current => _current.Value?.Value", generated);
        Assert.Contains("internal void Set(int value)", generated);
        Assert.Contains("class Holder", generated);
        Assert.Contains("BeginCapture()", generated);
    }

    // -------------------------------------------------------------------------
    // Interface without Current property — no generation
    // -------------------------------------------------------------------------

    [Fact]
    public void InterfaceWithoutCurrent_GeneratesNothing()
    {
        var source = """
            using NexusLabs.Needlr.AgentFramework;
            namespace TestApp
            {
                [AsyncLocalScoped]
                public interface IBadAccessor
                {
                    System.IDisposable BeginScope(string value);
                }
            }
            """;

        var files = RunGeneratorFiles(source);

        Assert.DoesNotContain(files, f => f.FilePath.Contains("AsyncLocalScoped"));
    }

    // -------------------------------------------------------------------------
    // Interface without IDisposable method — no generation
    // -------------------------------------------------------------------------

    [Fact]
    public void InterfaceWithoutScopeMethod_GeneratesNothing()
    {
        var source = """
            using NexusLabs.Needlr.AgentFramework;
            namespace TestApp
            {
                [AsyncLocalScoped]
                public interface IBadAccessor2
                {
                    string? Current { get; }
                }
            }
            """;

        var files = RunGeneratorFiles(source);

        Assert.DoesNotContain(files, f => f.FilePath.Contains("AsyncLocalScoped"));
    }

    // -------------------------------------------------------------------------
    // Class (not interface) with attribute — no generation
    // -------------------------------------------------------------------------

    [Fact]
    public void ClassWithAttribute_GeneratesNothing()
    {
        var source = """
            using NexusLabs.Needlr.AgentFramework;
            namespace TestApp
            {
                [AsyncLocalScoped]
                public class NotAnInterface
                {
                    public string? Current { get; }
                    public System.IDisposable BeginScope(string v) => null;
                }
            }
            """;

        var files = RunGeneratorFiles(source);

        Assert.DoesNotContain(files, f => f.FilePath.Contains("AsyncLocalScoped"));
    }

    // -------------------------------------------------------------------------
    // Custom type as value
    // -------------------------------------------------------------------------

    [Fact]
    public void CustomValueType_GeneratesCorrectType()
    {
        var source = """
            using NexusLabs.Needlr.AgentFramework;
            namespace TestApp
            {
                public record MyContext(string UserId);

                [AsyncLocalScoped]
                public interface IContextAccessor
                {
                    MyContext? Current { get; }
                    System.IDisposable BeginScope(MyContext value);
                }
            }
            """;

        var generated = RunGenerator(source);

        Assert.Contains("global::TestApp.MyContext", generated);
        Assert.Contains("internal sealed class ContextAccessor", generated);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static string RunGenerator(string source)
    {
        var files = RunGeneratorFiles(source);
        var asyncLocalFile = files.FirstOrDefault(f => f.FilePath.Contains("AsyncLocalScoped"));
        Assert.NotNull(asyncLocalFile);
        return asyncLocalFile!.Content;
    }

    private static GeneratedFile[] RunGeneratorFiles(string source)
    {
        var syntaxTrees = new[]
        {
            CSharpSyntaxTree.ParseText(AttributeStub),
            CSharpSyntaxTree.ParseText(source),
        };

        var references = Basic.Reference.Assemblies.Net100.References.All;

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            syntaxTrees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new AsyncLocalScopedGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(
            compilation, out var outputCompilation, out _);

        return outputCompilation.SyntaxTrees
            .Where(t => t.FilePath.EndsWith(".g.cs"))
            .Select(t => new GeneratedFile(t.FilePath, t.GetText().ToString()))
            .ToArray();
    }
}
