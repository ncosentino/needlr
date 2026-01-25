using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

using Xunit;

namespace NexusLabs.Needlr.Analyzers.Tests;

public sealed class CircularDependencyAnalyzerTests
{
    // Use shared test attributes that match the real package
    private static string Attributes => NeedlrTestAttributes.Core;

    [Fact]
    public async Task NoError_WhenNoCycle()
    {
        var code = @"
using NexusLabs.Needlr;

[Scoped]
public class ServiceA { }

[Scoped]
public class ServiceB
{
    public ServiceB(ServiceA a) { }
}

[Scoped]
public class ServiceC
{
    public ServiceC(ServiceB b) { }
}
" + Attributes;

        var test = new CSharpAnalyzerTest<CircularDependencyAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_WhenDirectCycle_A_B_A()
    {
        // The analyzer iterates types alphabetically, starting with ServiceA.
        // DFS from ServiceA: A→B, then B→A (back-edge). Cycle detected at ServiceB.
        var code = @"
using NexusLabs.Needlr;

[Scoped]
public class ServiceA
{
    public ServiceA(ServiceB b) { }
}

[Scoped]
public class {|#0:ServiceB|}
{
    public ServiceB(ServiceA a) { }
}
" + Attributes;

        var expected = new DiagnosticResult(DiagnosticIds.CircularDependency, DiagnosticSeverity.Error)
            .WithLocation(0)
            .WithArguments("ServiceA → ServiceB → ServiceA");

        var test = new CSharpAnalyzerTest<CircularDependencyAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics = { expected }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_WhenIndirectCycle_A_B_C_A()
    {
        // The analyzer iterates types alphabetically, starting with ServiceA.
        // DFS from ServiceA: A→B→C, then C→A (back-edge). Cycle detected at ServiceC.
        var code = @"
using NexusLabs.Needlr;

[Scoped]
public class ServiceA
{
    public ServiceA(ServiceB b) { }
}

[Scoped]
public class ServiceB
{
    public ServiceB(ServiceC c) { }
}

[Scoped]
public class {|#0:ServiceC|}
{
    public ServiceC(ServiceA a) { }
}
" + Attributes;

        var expected = new DiagnosticResult(DiagnosticIds.CircularDependency, DiagnosticSeverity.Error)
            .WithLocation(0)
            .WithArguments("ServiceA → ServiceB → ServiceC → ServiceA");

        var test = new CSharpAnalyzerTest<CircularDependencyAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics = { expected }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoError_WhenClassesNotRegistered()
    {
        // Classes without registration attributes are not analyzed
        var code = @"
public class ServiceA
{
    public ServiceA(ServiceB b) { }
}

public class ServiceB
{
    public ServiceB(ServiceA a) { }
}
" + Attributes;

        var test = new CSharpAnalyzerTest<CircularDependencyAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoError_WhenDependencyNotRegistered()
    {
        // ServiceB is not registered, so we can't track its dependencies
        var code = @"
using NexusLabs.Needlr;

public class ServiceB { }  // Not registered

[Scoped]
public class ServiceA
{
    public ServiceA(ServiceB b) { }
}
" + Attributes;

        var test = new CSharpAnalyzerTest<CircularDependencyAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoError_WhenPrimaryConstructorNoCycle()
    {
        var code = @"
using NexusLabs.Needlr;

[Scoped]
public class ServiceA { }

[Scoped]
public class ServiceB(ServiceA a);
" + Attributes;

        var test = new CSharpAnalyzerTest<CircularDependencyAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_WhenPrimaryConstructorCycle()
    {
        // Primary constructor syntax - C# 12
        // DFS: A→B, then B→A (back-edge). Cycle detected at ServiceB.
        var code = @"
using NexusLabs.Needlr;

[Scoped]
public class ServiceA(ServiceB b);

[Scoped]
public class {|#0:ServiceB|}(ServiceA a);
" + Attributes;

        var expected = new DiagnosticResult(DiagnosticIds.CircularDependency, DiagnosticSeverity.Error)
            .WithLocation(0)
            .WithArguments("ServiceA → ServiceB → ServiceA");

        var test = new CSharpAnalyzerTest<CircularDependencyAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics = { expected }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}
