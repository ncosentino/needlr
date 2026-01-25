using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

using Xunit;

namespace NexusLabs.Needlr.Analyzers.Tests;

public sealed class LifetimeMismatchAnalyzerTests
{
    // Use shared test attributes that match the real package
    private static string Attributes => NeedlrTestAttributes.Core;

    [Fact]
    public async Task NoWarning_WhenSingletonDependsOnSingleton()
    {
        var code = @"
using NexusLabs.Needlr;

[Singleton]
public class SingletonDependency { }

[Singleton]
public class SingletonService
{
    public SingletonService(SingletonDependency dep) { }
}
" + Attributes;

        var test = new CSharpAnalyzerTest<LifetimeMismatchAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoWarning_WhenScopedDependsOnSingleton()
    {
        var code = @"
using NexusLabs.Needlr;

[Singleton]
public class SingletonDependency { }

[Scoped]
public class ScopedService
{
    public ScopedService(SingletonDependency dep) { }
}
" + Attributes;

        var test = new CSharpAnalyzerTest<LifetimeMismatchAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoWarning_WhenTransientDependsOnAnything()
    {
        var code = @"
using NexusLabs.Needlr;

[Singleton]
public class SingletonDep { }

[Scoped]
public class ScopedDep { }

[Transient]
public class TransientService
{
    public TransientService(SingletonDep s, ScopedDep sc) { }
}
" + Attributes;

        var test = new CSharpAnalyzerTest<LifetimeMismatchAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Warning_WhenSingletonDependsOnScoped()
    {
        var code = @"
using NexusLabs.Needlr;

[Scoped]
public class ScopedDependency { }

[Singleton]
public class SingletonService
{
    public SingletonService({|#0:ScopedDependency dep|}) { }
}
" + Attributes;

        var expected = new DiagnosticResult(DiagnosticIds.LifetimeMismatch, DiagnosticSeverity.Warning)
            .WithLocation(0)
            .WithArguments("SingletonService", "Singleton", "ScopedDependency", "Scoped");

        var test = new CSharpAnalyzerTest<LifetimeMismatchAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics = { expected }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Warning_WhenSingletonDependsOnTransient()
    {
        var code = @"
using NexusLabs.Needlr;

[Transient]
public class TransientDependency { }

[Singleton]
public class SingletonService
{
    public SingletonService({|#0:TransientDependency dep|}) { }
}
" + Attributes;

        var expected = new DiagnosticResult(DiagnosticIds.LifetimeMismatch, DiagnosticSeverity.Warning)
            .WithLocation(0)
            .WithArguments("SingletonService", "Singleton", "TransientDependency", "Transient");

        var test = new CSharpAnalyzerTest<LifetimeMismatchAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics = { expected }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Warning_WhenScopedDependsOnTransient()
    {
        var code = @"
using NexusLabs.Needlr;

[Transient]
public class TransientDependency { }

[Scoped]
public class ScopedService
{
    public ScopedService({|#0:TransientDependency dep|}) { }
}
" + Attributes;

        var expected = new DiagnosticResult(DiagnosticIds.LifetimeMismatch, DiagnosticSeverity.Warning)
            .WithLocation(0)
            .WithArguments("ScopedService", "Scoped", "TransientDependency", "Transient");

        var test = new CSharpAnalyzerTest<LifetimeMismatchAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics = { expected }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Warning_WhenPrimaryConstructorHasMismatch()
    {
        var code = @"
using NexusLabs.Needlr;

[Scoped]
public class ScopedDependency { }

[Singleton]
public class SingletonService({|#0:ScopedDependency dep|});
" + Attributes;

        var expected = new DiagnosticResult(DiagnosticIds.LifetimeMismatch, DiagnosticSeverity.Warning)
            .WithLocation(0)
            .WithArguments("SingletonService", "Singleton", "ScopedDependency", "Scoped");

        var test = new CSharpAnalyzerTest<LifetimeMismatchAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics = { expected }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task MultipleWarnings_WhenMultipleMismatches()
    {
        var code = @"
using NexusLabs.Needlr;

[Scoped]
public class ScopedDep { }

[Transient]
public class TransientDep { }

[Singleton]
public class SingletonService
{
    public SingletonService({|#0:ScopedDep s|}, {|#1:TransientDep t|}) { }
}
" + Attributes;

        var expected1 = new DiagnosticResult(DiagnosticIds.LifetimeMismatch, DiagnosticSeverity.Warning)
            .WithLocation(0)
            .WithArguments("SingletonService", "Singleton", "ScopedDep", "Scoped");
        var expected2 = new DiagnosticResult(DiagnosticIds.LifetimeMismatch, DiagnosticSeverity.Warning)
            .WithLocation(1)
            .WithArguments("SingletonService", "Singleton", "TransientDep", "Transient");

        var test = new CSharpAnalyzerTest<LifetimeMismatchAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics = { expected1, expected2 }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Warning_WhenClassWithNoAttributeConsumesScopedDependency()
    {
        // Classes without lifetime attributes default to Singleton
        // So a class injecting a Scoped dependency should produce a captive dependency warning
        var code = @"
using NexusLabs.Needlr;

[Scoped]
public class ScopedDependency { }

// No lifetime attribute - defaults to Singleton
public class RegularService
{
    public RegularService({|#0:ScopedDependency dep|}) { }
}
" + Attributes;

        var expected = new DiagnosticResult(DiagnosticIds.LifetimeMismatch, DiagnosticSeverity.Warning)
            .WithLocation(0)
            .WithArguments("RegularService", "Singleton", "ScopedDependency", "Scoped");

        var test = new CSharpAnalyzerTest<LifetimeMismatchAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics = { expected }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoWarning_WhenDependencyDefaultsToSingleton()
    {
        // Classes without lifetime attributes default to Singleton
        // So Singleton consuming Singleton = OK (no mismatch)
        var code = @"
using NexusLabs.Needlr;

// No lifetime attribute - defaults to Singleton
public class DefaultDependency { }

[Singleton]
public class SingletonService
{
    public SingletonService(DefaultDependency dep) { }
}
" + Attributes;

        var test = new CSharpAnalyzerTest<LifetimeMismatchAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoWarning_WhenAbstractClass()
    {
        var code = @"
using NexusLabs.Needlr;

[Scoped]
public class ScopedDependency { }

[Singleton]
public abstract class AbstractService
{
    protected AbstractService(ScopedDependency dep) { }
}
" + Attributes;

        var test = new CSharpAnalyzerTest<LifetimeMismatchAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}
