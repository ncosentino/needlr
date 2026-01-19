using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

using Xunit;

namespace NexusLabs.Needlr.Analyzers.Tests;

public sealed class PluginConstructorDependenciesAnalyzerTests
{
    private const string NeedlrInterfaces = @"
namespace NexusLabs.Needlr
{
    public interface IServiceCollectionPlugin
    {
        void Configure(object options);
    }

    public interface IPostBuildServiceCollectionPlugin
    {
        void Configure(object options);
    }
}";

    [Fact]
    public async Task NoWarning_WhenPluginHasParameterlessConstructor()
    {
        var code = @"
using NexusLabs.Needlr;

public class MyPlugin : IServiceCollectionPlugin
{
    public MyPlugin() { }
    
    public void Configure(object options) { }
}
" + NeedlrInterfaces;

        var test = new CSharpAnalyzerTest<PluginConstructorDependenciesAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoWarning_WhenPluginHasImplicitParameterlessConstructor()
    {
        var code = @"
using NexusLabs.Needlr;

public class MyPlugin : IServiceCollectionPlugin
{
    public void Configure(object options) { }
}
" + NeedlrInterfaces;

        var test = new CSharpAnalyzerTest<PluginConstructorDependenciesAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Warning_WhenPluginHasConstructorWithParameters()
    {
        var code = @"
using NexusLabs.Needlr;

public class MyPlugin : IServiceCollectionPlugin
{
    public {|#0:MyPlugin|}(string dependency) { }
    
    public void Configure(object options) { }
}
" + NeedlrInterfaces;

        var expected = new DiagnosticResult(DiagnosticIds.PluginHasConstructorDependencies, DiagnosticSeverity.Warning)
            .WithLocation(0)
            .WithArguments("MyPlugin");

        var test = new CSharpAnalyzerTest<PluginConstructorDependenciesAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics = { expected }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Warning_WhenPostBuildPluginHasConstructorWithParameters()
    {
        var code = @"
using NexusLabs.Needlr;

public interface ILogger { }

public class MyPlugin : IPostBuildServiceCollectionPlugin
{
    public {|#0:MyPlugin|}(ILogger logger) { }
    
    public void Configure(object options) { }
}
" + NeedlrInterfaces;

        var expected = new DiagnosticResult(DiagnosticIds.PluginHasConstructorDependencies, DiagnosticSeverity.Warning)
            .WithLocation(0)
            .WithArguments("MyPlugin");

        var test = new CSharpAnalyzerTest<PluginConstructorDependenciesAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics = { expected }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoWarning_WhenPluginHasBothParameterlessAndParameterizedConstructors()
    {
        var code = @"
using NexusLabs.Needlr;

public class MyPlugin : IServiceCollectionPlugin
{
    public MyPlugin() { }
    public MyPlugin(string optional) { }
    
    public void Configure(object options) { }
}
" + NeedlrInterfaces;

        var test = new CSharpAnalyzerTest<PluginConstructorDependenciesAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoWarning_WhenNonPluginClassHasConstructorParameters()
    {
        var code = @"
public class RegularClass
{
    public RegularClass(string dependency) { }
}
" + NeedlrInterfaces;

        var test = new CSharpAnalyzerTest<PluginConstructorDependenciesAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoWarning_WhenAbstractPluginHasConstructorParameters()
    {
        var code = @"
using NexusLabs.Needlr;

public abstract class BasePlugin : IServiceCollectionPlugin
{
    protected BasePlugin(string config) { }
    
    public abstract void Configure(object options);
}
" + NeedlrInterfaces;

        var test = new CSharpAnalyzerTest<PluginConstructorDependenciesAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Warning_WhenPluginHasMultipleParameterizedConstructors()
    {
        var code = @"
using NexusLabs.Needlr;

public class MyPlugin : IServiceCollectionPlugin
{
    public {|#0:MyPlugin|}(string a) { }
    public {|#1:MyPlugin|}(string a, int b) { }
    
    public void Configure(object options) { }
}
" + NeedlrInterfaces;

        var expected1 = new DiagnosticResult(DiagnosticIds.PluginHasConstructorDependencies, DiagnosticSeverity.Warning)
            .WithLocation(0)
            .WithArguments("MyPlugin");
        var expected2 = new DiagnosticResult(DiagnosticIds.PluginHasConstructorDependencies, DiagnosticSeverity.Warning)
            .WithLocation(1)
            .WithArguments("MyPlugin");

        var test = new CSharpAnalyzerTest<PluginConstructorDependenciesAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics = { expected1, expected2 }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}
