using System.Threading.Tasks;

using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

using Xunit;

namespace NexusLabs.Needlr.Analyzers.Tests;

public sealed class LazyResolutionAnalyzerTests
{
    // Use shared test attributes that match the real package
    private static string Attributes => NeedlrTestAttributes.All;

    [Fact]
    public async Task NoWarning_WhenGenerateTypeRegistryNotPresent()
    {
        var code = @"
using System;

public class Service
{
    public Service(Lazy<IUnknownService> lazy) { }
}

public interface IUnknownService { }
";

        var test = new CSharpAnalyzerTest<LazyResolutionAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoWarning_WhenLazyTypeHasImplementation()
    {
        var code = @"
using System;
using NexusLabs.Needlr;
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

public interface IMyService { }

public class MyService : IMyService { }

[Singleton]
public class Consumer
{
    public Consumer(Lazy<IMyService> lazy) { }
}
" + Attributes;

        var test = new CSharpAnalyzerTest<LazyResolutionAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoWarning_WhenLazyTypeHasLifetimeAttribute()
    {
        var code = @"
using System;
using NexusLabs.Needlr;
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

[Singleton]
public class MySingleton { }

public class Consumer
{
    public Consumer(Lazy<MySingleton> lazy) { }
}
" + Attributes;

        var test = new CSharpAnalyzerTest<LazyResolutionAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Info_WhenLazyTypeNotDiscovered()
    {
        var code = @"
using System;
using NexusLabs.Needlr;
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

public interface IUnknownService { }

public class Consumer
{
    public Consumer({|#0:Lazy<IUnknownService>|} lazy) { }
}
" + Attributes;

        var test = new CSharpAnalyzerTest<LazyResolutionAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics =
            {
                new DiagnosticResult(DiagnosticIds.LazyResolutionUnknown, Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                    .WithLocation(0)
                    .WithArguments("IUnknownService")
            }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoWarning_WhenLazyTypeIsFrameworkType()
    {
        var code = @"
using System;
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

public class Consumer
{
    public Consumer(Lazy<IDisposable> lazy) { }
}
" + Attributes;

        var test = new CSharpAnalyzerTest<LazyResolutionAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Info_WhenLazyTypeExcludedWithDoNotInject()
    {
        var code = @"
using System;
using NexusLabs.Needlr;
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

public interface IExcludedService { }

[DoNotInject]
public class ExcludedService : IExcludedService { }

public class Consumer
{
    public Consumer({|#0:Lazy<IExcludedService>|} lazy) { }
}
" + Attributes;

        var test = new CSharpAnalyzerTest<LazyResolutionAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics =
            {
                new DiagnosticResult(DiagnosticIds.LazyResolutionUnknown, Microsoft.CodeAnalysis.DiagnosticSeverity.Info)
                    .WithLocation(0)
                    .WithArguments("IExcludedService")
            }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}
