using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

using Xunit;

namespace NexusLabs.Needlr.Analyzers.Tests;

public sealed class DeferToContainerInGeneratedCodeAnalyzerTests
{
    private const string NeedlrAttribute = @"
namespace NexusLabs.Needlr
{
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public sealed class DeferToContainerAttribute : System.Attribute
    {
        public DeferToContainerAttribute(params System.Type[] constructorParameterTypes) { }
    }
}";

    [Fact]
    public async Task NoError_WhenDeferToContainerInUserCode()
    {
        var code = @"
using NexusLabs.Needlr;

public interface ICacheProvider { }

[DeferToContainer(typeof(ICacheProvider))]
public partial class CacheService { }
" + NeedlrAttribute;

        var test = new CSharpAnalyzerTest<DeferToContainerInGeneratedCodeAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoError_WhenNoDeferToContainerAttribute()
    {
        var code = @"
public partial class RegularService { }
" + NeedlrAttribute;

        var test = new CSharpAnalyzerTest<DeferToContainerInGeneratedCodeAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_WhenDeferToContainerInGeneratedFile()
    {
        // Note: The test framework uses the file path pattern to simulate generated code
        var generatedCode = @"
using NexusLabs.Needlr;

public interface ICacheProvider { }

[{|#0:DeferToContainer(typeof(ICacheProvider))|}]
public partial class EngageFeedCacheProvider { }
" + NeedlrAttribute;

        var expected = new DiagnosticResult(DiagnosticIds.DeferToContainerInGeneratedCode, DiagnosticSeverity.Error)
            .WithLocation(0)
            .WithArguments("EngageFeedCacheProvider");

        var test = new CSharpAnalyzerTest<DeferToContainerInGeneratedCodeAnalyzer, DefaultVerifier>
        {
            TestState =
            {
                Sources =
                {
                    // Add the generated file with .g.cs extension
                    ("/0/Test.g.cs", generatedCode)
                }
            },
            ExpectedDiagnostics = { expected }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_WhenDeferToContainerInGeneratedDesignerFile()
    {
        var generatedCode = @"
using NexusLabs.Needlr;

public interface IService { }

[{|#0:DeferToContainer(typeof(IService))|}]
public partial class GeneratedService { }
" + NeedlrAttribute;

        var expected = new DiagnosticResult(DiagnosticIds.DeferToContainerInGeneratedCode, DiagnosticSeverity.Error)
            .WithLocation(0)
            .WithArguments("GeneratedService");

        var test = new CSharpAnalyzerTest<DeferToContainerInGeneratedCodeAnalyzer, DefaultVerifier>
        {
            TestState =
            {
                Sources =
                {
                    ("/0/Test.designer.cs", generatedCode)
                }
            },
            ExpectedDiagnostics = { expected }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_WhenDeferToContainerInDotGeneratedFile()
    {
        var generatedCode = @"
using NexusLabs.Needlr;

public interface IRepository { }

[{|#0:DeferToContainer(typeof(IRepository))|}]
public partial class DataService { }
" + NeedlrAttribute;

        var expected = new DiagnosticResult(DiagnosticIds.DeferToContainerInGeneratedCode, DiagnosticSeverity.Error)
            .WithLocation(0)
            .WithArguments("DataService");

        var test = new CSharpAnalyzerTest<DeferToContainerInGeneratedCodeAnalyzer, DefaultVerifier>
        {
            TestState =
            {
                Sources =
                {
                    ("/0/Test.generated.cs", generatedCode)
                }
            },
            ExpectedDiagnostics = { expected }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoError_WhenDeferToContainerWithFullNameInUserCode()
    {
        var code = @"
public interface ICacheProvider { }

[NexusLabs.Needlr.DeferToContainer(typeof(ICacheProvider))]
public partial class CacheService { }
" + NeedlrAttribute;

        var test = new CSharpAnalyzerTest<DeferToContainerInGeneratedCodeAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_WhenDeferToContainerWithFullNameInGeneratedFile()
    {
        var generatedCode = @"
public interface ICacheProvider { }

[{|#0:NexusLabs.Needlr.DeferToContainer(typeof(ICacheProvider))|}]
public partial class EngageFeedCacheProvider { }
" + NeedlrAttribute;

        var expected = new DiagnosticResult(DiagnosticIds.DeferToContainerInGeneratedCode, DiagnosticSeverity.Error)
            .WithLocation(0)
            .WithArguments("EngageFeedCacheProvider");

        var test = new CSharpAnalyzerTest<DeferToContainerInGeneratedCodeAnalyzer, DefaultVerifier>
        {
            TestState =
            {
                Sources =
                {
                    ("/0/Test.g.cs", generatedCode)
                }
            },
            ExpectedDiagnostics = { expected }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_WhenDeferToContainerInObjGeneratedFolder()
    {
        var generatedCode = @"
using NexusLabs.Needlr;

public interface ILogger { }

[{|#0:DeferToContainer(typeof(ILogger))|}]
public partial class LoggingService { }
" + NeedlrAttribute;

        var expected = new DiagnosticResult(DiagnosticIds.DeferToContainerInGeneratedCode, DiagnosticSeverity.Error)
            .WithLocation(0)
            .WithArguments("LoggingService");

        var test = new CSharpAnalyzerTest<DeferToContainerInGeneratedCodeAnalyzer, DefaultVerifier>
        {
            TestState =
            {
                Sources =
                {
                    // Simulate the typical source generator output path
                    ("/Project/obj/Debug/net9.0/generated/MyGenerator/LoggingService.cs", generatedCode)
                }
            },
            ExpectedDiagnostics = { expected }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoError_WhenOtherAttributeInGeneratedFile()
    {
        // Other attributes in generated files should not trigger this diagnostic
        var generatedCode = @"
[System.Serializable]
public partial class SomeClass { }
" + NeedlrAttribute;

        var test = new CSharpAnalyzerTest<DeferToContainerInGeneratedCodeAnalyzer, DefaultVerifier>
        {
            TestState =
            {
                Sources =
                {
                    ("/0/Test.g.cs", generatedCode)
                }
            }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_WhenDeferToContainerWithMultipleTypesInGeneratedFile()
    {
        var generatedCode = @"
using NexusLabs.Needlr;

public interface ICacheProvider { }
public interface ILogger { }

[{|#0:DeferToContainer(typeof(ICacheProvider), typeof(ILogger))|}]
public partial class MultiDependencyService { }
" + NeedlrAttribute;

        var expected = new DiagnosticResult(DiagnosticIds.DeferToContainerInGeneratedCode, DiagnosticSeverity.Error)
            .WithLocation(0)
            .WithArguments("MultiDependencyService");

        var test = new CSharpAnalyzerTest<DeferToContainerInGeneratedCodeAnalyzer, DefaultVerifier>
        {
            TestState =
            {
                Sources =
                {
                    ("/0/Test.g.cs", generatedCode)
                }
            },
            ExpectedDiagnostics = { expected }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}
