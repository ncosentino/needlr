using System.Threading.Tasks;

using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

using Xunit;

namespace NexusLabs.Needlr.Analyzers.Tests;

public sealed class DisposableCaptiveDependencyAnalyzerTests
{
    private static string Attributes => NeedlrTestAttributes.Core;

    [Fact]
    public async Task Error_WhenSingletonDependsOnScopedDisposable()
    {
        var code = @"
using System;
using NexusLabs.Needlr;

[Scoped]
public class ScopedDisposable : IDisposable
{
    public void Dispose() { }
}

[Singleton]
public class SingletonService
{
    public SingletonService({|#0:ScopedDisposable dep|}) { }
}
" + Attributes;

        var test = new CSharpAnalyzerTest<DisposableCaptiveDependencyAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics =
            {
                new DiagnosticResult(DiagnosticDescriptors.DisposableCaptiveDependency)
                    .WithLocation(0)
                    .WithArguments("SingletonService", "Singleton", "ScopedDisposable", "Scoped", "IDisposable")
            }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_WhenSingletonDependsOnTransientDisposable()
    {
        var code = @"
using System;
using NexusLabs.Needlr;

[Transient]
public class TransientDisposable : IDisposable
{
    public void Dispose() { }
}

[Singleton]
public class SingletonService
{
    public SingletonService({|#0:TransientDisposable dep|}) { }
}
" + Attributes;

        var test = new CSharpAnalyzerTest<DisposableCaptiveDependencyAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics =
            {
                new DiagnosticResult(DiagnosticDescriptors.DisposableCaptiveDependency)
                    .WithLocation(0)
                    .WithArguments("SingletonService", "Singleton", "TransientDisposable", "Transient", "IDisposable")
            }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_WhenScopedDependsOnTransientDisposable()
    {
        var code = @"
using System;
using NexusLabs.Needlr;

[Transient]
public class TransientDisposable : IDisposable
{
    public void Dispose() { }
}

[Scoped]
public class ScopedService
{
    public ScopedService({|#0:TransientDisposable dep|}) { }
}
" + Attributes;

        var test = new CSharpAnalyzerTest<DisposableCaptiveDependencyAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics =
            {
                new DiagnosticResult(DiagnosticDescriptors.DisposableCaptiveDependency)
                    .WithLocation(0)
                    .WithArguments("ScopedService", "Scoped", "TransientDisposable", "Transient", "IDisposable")
            }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_WhenSingletonDependsOnScopedAsyncDisposable()
    {
        var code = @"
using System;
using System.Threading.Tasks;
using NexusLabs.Needlr;

[Scoped]
public class ScopedAsyncDisposable : IAsyncDisposable
{
    public ValueTask DisposeAsync() => default;
}

[Singleton]
public class SingletonService
{
    public SingletonService({|#0:ScopedAsyncDisposable dep|}) { }
}
" + Attributes;

        var test = new CSharpAnalyzerTest<DisposableCaptiveDependencyAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics =
            {
                new DiagnosticResult(DiagnosticDescriptors.DisposableCaptiveDependency)
                    .WithLocation(0)
                    .WithArguments("SingletonService", "Singleton", "ScopedAsyncDisposable", "Scoped", "IAsyncDisposable")
            }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoError_WhenSingletonDependsOnSingletonDisposable()
    {
        var code = @"
using System;
using NexusLabs.Needlr;

[Singleton]
public class SingletonDisposable : IDisposable
{
    public void Dispose() { }
}

[Singleton]
public class SingletonService
{
    public SingletonService(SingletonDisposable dep) { }
}
" + Attributes;

        var test = new CSharpAnalyzerTest<DisposableCaptiveDependencyAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoError_WhenScopedDependsOnScopedDisposable()
    {
        var code = @"
using System;
using NexusLabs.Needlr;

[Scoped]
public class ScopedDisposable : IDisposable
{
    public void Dispose() { }
}

[Scoped]
public class ScopedService
{
    public ScopedService(ScopedDisposable dep) { }
}
" + Attributes;

        var test = new CSharpAnalyzerTest<DisposableCaptiveDependencyAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoError_WhenTransientDependsOnAnyDisposable()
    {
        var code = @"
using System;
using NexusLabs.Needlr;

[Scoped]
public class ScopedDisposable : IDisposable
{
    public void Dispose() { }
}

[Transient]
public class TransientService
{
    public TransientService(ScopedDisposable dep) { }
}
" + Attributes;

        var test = new CSharpAnalyzerTest<DisposableCaptiveDependencyAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoError_WhenDependencyIsNotDisposable()
    {
        // Non-disposable captive dependencies are handled by NDLRCOR005
        var code = @"
using NexusLabs.Needlr;

[Scoped]
public class ScopedDependency { }

[Singleton]
public class SingletonService
{
    public SingletonService(ScopedDependency dep) { }
}
" + Attributes;

        var test = new CSharpAnalyzerTest<DisposableCaptiveDependencyAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoError_WhenDependencyHasNoExplicitLifetime()
    {
        // Conservative: no explicit lifetime = no error (avoid false positives)
        var code = @"
using System;
using NexusLabs.Needlr;

public class DisposableDependency : IDisposable
{
    public void Dispose() { }
}

[Singleton]
public class SingletonService
{
    public SingletonService(DisposableDependency dep) { }
}
" + Attributes;

        var test = new CSharpAnalyzerTest<DisposableCaptiveDependencyAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoError_WhenConsumerHasNoExplicitLifetime()
    {
        // Conservative: no explicit lifetime = no error (avoid false positives)
        var code = @"
using System;
using NexusLabs.Needlr;

[Scoped]
public class ScopedDisposable : IDisposable
{
    public void Dispose() { }
}

public class ServiceWithImplicitLifetime
{
    public ServiceWithImplicitLifetime(ScopedDisposable dep) { }
}
" + Attributes;

        var test = new CSharpAnalyzerTest<DisposableCaptiveDependencyAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoError_WhenUsingFuncFactory()
    {
        // Factory patterns are safe - scoped dependency is resolved fresh each time
        var code = @"
using System;
using NexusLabs.Needlr;

[Scoped]
public class ScopedDisposable : IDisposable
{
    public void Dispose() { }
}

[Singleton]
public class SingletonService
{
    public SingletonService(Func<ScopedDisposable> factory) { }
}
" + Attributes;

        var test = new CSharpAnalyzerTest<DisposableCaptiveDependencyAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoError_WhenUsingLazy()
    {
        var code = @"
using System;
using NexusLabs.Needlr;

[Scoped]
public class ScopedDisposable : IDisposable
{
    public void Dispose() { }
}

[Singleton]
public class SingletonService
{
    public SingletonService(Lazy<ScopedDisposable> lazy) { }
}
" + Attributes;

        var test = new CSharpAnalyzerTest<DisposableCaptiveDependencyAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoError_WhenDependencyIsInterface()
    {
        // Can't determine concrete type from interface, skip to avoid false positives
        var code = @"
using System;
using NexusLabs.Needlr;

public interface IScopedDisposable : IDisposable { }

[Singleton]
public class SingletonService
{
    public SingletonService(IScopedDisposable dep) { }
}
" + Attributes;

        var test = new CSharpAnalyzerTest<DisposableCaptiveDependencyAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_WithPrimaryConstructor()
    {
        var code = @"
using System;
using NexusLabs.Needlr;

[Scoped]
public class ScopedDisposable : IDisposable
{
    public void Dispose() { }
}

[Singleton]
public class SingletonService({|#0:ScopedDisposable dep|});
" + Attributes;

        var test = new CSharpAnalyzerTest<DisposableCaptiveDependencyAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics =
            {
                new DiagnosticResult(DiagnosticDescriptors.DisposableCaptiveDependency)
                    .WithLocation(0)
                    .WithArguments("SingletonService", "Singleton", "ScopedDisposable", "Scoped", "IDisposable")
            }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_WithMultipleDependencies_OnlyReportsDisposableOnes()
    {
        var code = @"
using System;
using NexusLabs.Needlr;

[Scoped]
public class ScopedNonDisposable { }

[Scoped]
public class ScopedDisposable : IDisposable
{
    public void Dispose() { }
}

[Singleton]
public class SingletonService
{
    public SingletonService(ScopedNonDisposable nonDisp, {|#0:ScopedDisposable disp|}) { }
}
" + Attributes;

        var test = new CSharpAnalyzerTest<DisposableCaptiveDependencyAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics =
            {
                new DiagnosticResult(DiagnosticDescriptors.DisposableCaptiveDependency)
                    .WithLocation(0)
                    .WithArguments("SingletonService", "Singleton", "ScopedDisposable", "Scoped", "IDisposable")
            }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoError_WhenClassIsAbstract()
    {
        var code = @"
using System;
using NexusLabs.Needlr;

[Scoped]
public class ScopedDisposable : IDisposable
{
    public void Dispose() { }
}

[Singleton]
public abstract class AbstractSingletonService
{
    protected AbstractSingletonService(ScopedDisposable dep) { }
}
" + Attributes;

        var test = new CSharpAnalyzerTest<DisposableCaptiveDependencyAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}
