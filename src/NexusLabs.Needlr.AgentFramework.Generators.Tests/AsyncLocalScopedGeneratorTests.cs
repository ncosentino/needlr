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
    // Property proxies — mutable variant with read/write properties
    // -------------------------------------------------------------------------

    [Fact]
    public void MutableVariant_GeneratesPropertyProxiesForReadWriteProperties()
    {
        var source = """
            using NexusLabs.Needlr.AgentFramework;
            namespace TestApp
            {
                public class RunContext
                {
                    public string? Title { get; set; }
                    public int Count { get; set; }
                }

                [AsyncLocalScoped(Mutable = true)]
                public interface IRunAccessor
                {
                    RunContext? Current { get; }
                    System.IDisposable BeginScope(RunContext value);
                    string? Title { get; set; }
                    int Count { get; set; }
                }
            }
            """;

        var generated = RunGenerator(source);

        // Getter proxies
        Assert.Contains("get => _current.Value?.Value?.Title;", generated);
        Assert.Contains("get => _current.Value?.Value?.Count ?? default;", generated);

        // Setter proxies — use partial match to avoid fully-qualified type issues
        Assert.Contains("h.Title = value;", generated);
        Assert.Contains("h.Count = value;", generated);
    }

    // -------------------------------------------------------------------------
    // Property proxies — mutable variant with read-only properties
    // -------------------------------------------------------------------------

    [Fact]
    public void MutableVariant_GeneratesGetterOnlyForReadOnlyProperties()
    {
        var source = """
            using NexusLabs.Needlr.AgentFramework;
            namespace TestApp
            {
                public class RunInfo
                {
                    public string? Id { get; }
                }

                [AsyncLocalScoped(Mutable = true)]
                public interface IRunInfoAccessor
                {
                    RunInfo? Current { get; }
                    System.IDisposable BeginScope(RunInfo value);
                    string? Id { get; }
                }
            }
            """;

        var generated = RunGenerator(source);

        Assert.Contains("Id => _current.Value?.Value?.Id;", generated);
        // Should NOT contain a setter block for read-only properties
        Assert.DoesNotContain("h.Id = value;", generated);
    }

    // -------------------------------------------------------------------------
    // Property proxies — simple variant
    // -------------------------------------------------------------------------

    [Fact]
    public void SimpleVariant_GeneratesPropertyProxies()
    {
        var source = """
            using NexusLabs.Needlr.AgentFramework;
            namespace TestApp
            {
                public class SimpleCtx
                {
                    public string? Name { get; set; }
                    public bool IsReady { get; }
                }

                [AsyncLocalScoped]
                public interface ISimpleCtxAccessor
                {
                    SimpleCtx? Current { get; }
                    System.IDisposable BeginScope(SimpleCtx value);
                    string? Name { get; set; }
                    bool IsReady { get; }
                }
            }
            """;

        var generated = RunGenerator(source);

        // Read/write proxy
        Assert.Contains("get => _current.Value?.Name;", generated);
        Assert.Contains("h.Name = value;", generated);

        // Read-only proxy (value type gets ?? default)
        Assert.Contains("IsReady => _current.Value?.IsReady ?? default;", generated);
        Assert.DoesNotContain("h.IsReady = value;", generated);
    }

    // -------------------------------------------------------------------------
    // No proxy properties — no extra output
    // -------------------------------------------------------------------------

    [Fact]
    public void NoExtraProperties_NoProxiesGenerated()
    {
        var source = """
            using NexusLabs.Needlr.AgentFramework;
            namespace TestApp
            {
                [AsyncLocalScoped(Mutable = true)]
                public interface IMinimalAccessor
                {
                    int? Current { get; }
                    System.IDisposable BeginCapture();
                }
            }
            """;

        var generated = RunGenerator(source);

        // Should have Current but no extra property proxies
        Assert.Contains("Current => _current.Value?.Value", generated);
        // The generated code has no proxy properties beyond Current, the scope method,
        // Set, the Scope constructor, and the Holder field — verify no proxy getters/setters
        Assert.DoesNotContain("get =>", generated);
        Assert.DoesNotContain("set {", generated);
    }

    // -------------------------------------------------------------------------
    // Property proxies — generated code compiles cleanly
    // -------------------------------------------------------------------------

    [Fact]
    public void PropertyProxies_GeneratedCodeCompiles()
    {
        var source = """
            using NexusLabs.Needlr.AgentFramework;
            namespace TestApp
            {
                public class PipelineRun
                {
                    public string? Title { get; set; }
                    public int Priority { get; set; }
                    public bool IsComplete { get; }
                }

                [AsyncLocalScoped(Mutable = true)]
                public interface IPipelineRunAccessor
                {
                    PipelineRun? Current { get; }
                    System.IDisposable BeginScope(PipelineRun value);
                    string? Title { get; set; }
                    int Priority { get; set; }
                    bool IsComplete { get; }
                }
            }
            """;

        var diagnostics = RunGeneratorAndGetDiagnostics(source);

        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        Assert.Empty(errors);
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

    private static Diagnostic[] RunGeneratorAndGetDiagnostics(string source)
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

        return outputCompilation.GetDiagnostics().ToArray();
    }
}
