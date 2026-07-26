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

    [Fact]
    public async Task Warning_WhenPluginImplementsCustomInterfaceInheritingPluginInterface()
    {
        var code = @"
using NexusLabs.Needlr;

public interface IMyPlugin : IServiceCollectionPlugin { }

public class MyPlugin : IMyPlugin
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
    public async Task Warning_WhenBaseClassImplementsPluginInterface()
    {
        var code = @"
using NexusLabs.Needlr;

public abstract class BasePlugin : IServiceCollectionPlugin
{
    public abstract void Configure(object options);
}

public class MyPlugin : BasePlugin
{
    public {|#0:MyPlugin|}(string dependency) { }

    public override void Configure(object options) { }
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
    public async Task Warning_WhenPluginInterfaceIsNotTheFirstBaseListEntry()
    {
        var code = @"
using NexusLabs.Needlr;

public interface IMarker { }

public class MyPlugin : IMarker, IServiceCollectionPlugin
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
    public async Task Warning_WhenPluginInterfaceIsReferencedByQualifiedName()
    {
        var code = @"
public class MyPlugin : NexusLabs.Needlr.IServiceCollectionPlugin
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
    public async Task NoWarning_WhenSameNamedInterfaceIsInAnotherNamespace()
    {
        var code = @"
namespace OtherVendor
{
    public interface IServiceCollectionPlugin
    {
        void Configure(object options);
    }
}

public class MyPlugin : OtherVendor.IServiceCollectionPlugin
{
    public MyPlugin(string dependency) { }

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
    public async Task NoWarning_WhenSameNamedInterfaceInAnotherNamespaceIsReferencedBySimpleName()
    {
        var code = @"
using OtherVendor;

namespace OtherVendor
{
    public interface IServiceCollectionPlugin
    {
        void Configure(object options);
    }
}

public class MyPlugin : IServiceCollectionPlugin
{
    public MyPlugin(string dependency) { }

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
    public async Task NoWarning_WhenClassHasNoBaseList()
    {
        var code = @"
public class MyPlugin
{
    public MyPlugin(string dependency) { }
}
" + NeedlrInterfaces;

        var test = new CSharpAnalyzerTest<PluginConstructorDependenciesAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoWarning_WhenPluginInterfaceSimpleNameCannotBeResolved()
    {
        // A bare, unresolved identifier is deliberately not matched on its simple name:
        // it may refer to an unrelated same-named interface from any namespace, so the
        // analyzer stays silent rather than reporting a false positive on broken code.
        var code = @"
public class MyPlugin : {|CS0246:IServiceCollectionPlugin|}
{
    public MyPlugin(string dependency) { }
}
";

        var test = new CSharpAnalyzerTest<PluginConstructorDependenciesAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Warning_WhenPluginInterfaceQualifiedNameCannotBeResolved()
    {
        // An unresolved but namespace-qualified base type still yields an error symbol
        // carrying the Needlr namespace and plugin interface name, which is unambiguous
        // enough to diagnose while the code is still incomplete.
        var code = @"
public class MyPlugin : NexusLabs.Needlr.{|#0:IServiceCollectionPlugin|}
{
    public {|#1:MyPlugin|}(string dependency) { }
}

namespace NexusLabs.Needlr
{
    public interface IUnrelated { }
}
";

        var unresolvedInterface = DiagnosticResult.CompilerError("CS0234")
            .WithLocation(0)
            .WithArguments("IServiceCollectionPlugin", "NexusLabs.Needlr");
        var expected = new DiagnosticResult(DiagnosticIds.PluginHasConstructorDependencies, DiagnosticSeverity.Warning)
            .WithLocation(1)
            .WithArguments("MyPlugin");

        var test = new CSharpAnalyzerTest<PluginConstructorDependenciesAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics = { unresolvedInterface, expected }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}
