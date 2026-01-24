using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

using Xunit;

namespace NexusLabs.Needlr.Analyzers.Tests;

public sealed class LifetimeMismatchAnalyzerTests
{
    private const string NeedlrAttributes = @"
namespace NexusLabs.Needlr
{
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class SingletonAttribute : System.Attribute { }

    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class ScopedAttribute : System.Attribute { }

    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class TransientAttribute : System.Attribute { }

    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class RegisterAsAttribute : System.Attribute
    {
        public RegisterAsAttribute(ServiceLifetime lifetime) { }
        public RegisterAsAttribute(System.Type serviceType, ServiceLifetime lifetime = ServiceLifetime.Transient) { }
    }

    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class AutoRegisterAttribute : System.Attribute
    {
        public ServiceLifetime Lifetime { get; set; } = ServiceLifetime.Transient;
    }

    public enum ServiceLifetime
    {
        Singleton = 0,
        Scoped = 1,
        Transient = 2
    }
}";

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
" + NeedlrAttributes;

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
" + NeedlrAttributes;

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
" + NeedlrAttributes;

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
" + NeedlrAttributes;

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
" + NeedlrAttributes;

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
" + NeedlrAttributes;

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
" + NeedlrAttributes;

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
" + NeedlrAttributes;

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
    public async Task NoWarning_WhenClassHasNoLifetimeAttribute()
    {
        var code = @"
using NexusLabs.Needlr;

[Scoped]
public class ScopedDependency { }

// No lifetime attribute - not analyzed
public class RegularService
{
    public RegularService(ScopedDependency dep) { }
}
" + NeedlrAttributes;

        var test = new CSharpAnalyzerTest<LifetimeMismatchAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoWarning_WhenDependencyHasNoLifetimeAttribute()
    {
        var code = @"
using NexusLabs.Needlr;

// No lifetime attribute - unknown lifetime
public class UnknownDependency { }

[Singleton]
public class SingletonService
{
    public SingletonService(UnknownDependency dep) { }
}
" + NeedlrAttributes;

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
" + NeedlrAttributes;

        var test = new CSharpAnalyzerTest<LifetimeMismatchAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    // === Source-gen parity tests for RegisterAs and AutoRegister ===

    [Fact]
    public async Task Warning_WhenRegisterAsSingletonDependsOnScoped()
    {
        var code = @"
using NexusLabs.Needlr;

[Scoped]
public class ScopedDependency { }

[RegisterAs(ServiceLifetime.Singleton)]
public class SingletonService
{
    public SingletonService({|#0:ScopedDependency dep|}) { }
}
" + NeedlrAttributes;

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
    public async Task NoWarning_WhenRegisterAsScopedDependsOnSingleton()
    {
        var code = @"
using NexusLabs.Needlr;

[Singleton]
public class SingletonDependency { }

[RegisterAs(typeof(IScopedService), ServiceLifetime.Scoped)]
public class ScopedService : IScopedService
{
    public ScopedService(SingletonDependency dep) { }
}

public interface IScopedService { }
" + NeedlrAttributes;

        var test = new CSharpAnalyzerTest<LifetimeMismatchAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Warning_WhenAutoRegisterSingletonDependsOnTransient()
    {
        var code = @"
using NexusLabs.Needlr;

[Transient]
public class TransientDependency { }

[AutoRegister(Lifetime = ServiceLifetime.Singleton)]
public class SingletonService
{
    public SingletonService({|#0:TransientDependency dep|}) { }
}
" + NeedlrAttributes;

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
    public async Task NoWarning_WhenAutoRegisterTransientDependsOnSingleton()
    {
        var code = @"
using NexusLabs.Needlr;

[Singleton]
public class SingletonDependency { }

[AutoRegister(Lifetime = ServiceLifetime.Transient)]
public class TransientService
{
    public TransientService(SingletonDependency dep) { }
}
" + NeedlrAttributes;

        var test = new CSharpAnalyzerTest<LifetimeMismatchAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Warning_MixedAttributes_RegisterAsAndSingleton()
    {
        var code = @"
using NexusLabs.Needlr;

// Dependency uses simple [Scoped] attribute
[Scoped]
public class ScopedDependency { }

// Consumer uses [RegisterAs] with Singleton
[RegisterAs(ServiceLifetime.Singleton)]
public class MixedService
{
    public MixedService({|#0:ScopedDependency dep|}) { }
}
" + NeedlrAttributes;

        var expected = new DiagnosticResult(DiagnosticIds.LifetimeMismatch, DiagnosticSeverity.Warning)
            .WithLocation(0)
            .WithArguments("MixedService", "Singleton", "ScopedDependency", "Scoped");

        var test = new CSharpAnalyzerTest<LifetimeMismatchAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics = { expected }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}
