using System.Threading.Tasks;

using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

using Xunit;

namespace NexusLabs.Needlr.Analyzers.Tests;

public sealed class LazyResolutionAnalyzerTests
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
    public class DoNotInjectAttribute : System.Attribute { }
}

namespace NexusLabs.Needlr.Generators
{
    [System.AttributeUsage(System.AttributeTargets.Assembly)]
    public class GenerateTypeRegistryAttribute : System.Attribute
    {
        public string[]? IncludeNamespacePrefixes { get; set; }
        public bool IncludeSelf { get; set; } = true;
    }
}";

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
" + NeedlrAttributes;

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
" + NeedlrAttributes;

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
" + NeedlrAttributes;

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
" + NeedlrAttributes;

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
" + NeedlrAttributes;

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
