using System.Threading.Tasks;

using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

using Xunit;

namespace NexusLabs.Needlr.Generators.Tests.Provider;

/// <summary>
/// Tests for ProviderAttributeAnalyzer diagnostics:
/// - NDLRGEN031: [Provider] on class requires partial modifier
/// - NDLRGEN032: [Provider] interface has invalid member
/// - NDLRGEN033: Provider property uses concrete type
/// - NDLRGEN034: Circular provider dependency detected
/// </summary>
public sealed class ProviderAttributeAnalyzerTests
{
    private static string Attributes => NeedlrTestAttributes.AllWithProvider;

    [Fact]
    public async Task Error_WhenProviderClassNotPartial()
    {
        var code = @"
using NexusLabs.Needlr.Generators;

public interface IService { }

[{|#0:Provider(typeof(IService))|}]
public class MyProvider { }
" + Attributes;

        var test = new CSharpAnalyzerTest<ProviderAttributeAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics =
            {
                new DiagnosticResult("NDLRGEN031", Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                    .WithLocation(0)
                    .WithArguments("MyProvider")
            }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoError_WhenProviderClassIsPartial()
    {
        var code = @"
using NexusLabs.Needlr.Generators;

public interface IService { }

[Provider(typeof(IService))]
public partial class MyProvider { }
" + Attributes;

        var test = new CSharpAnalyzerTest<ProviderAttributeAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoError_WhenProviderInterfaceHasGetOnlyProperties()
    {
        var code = @"
using NexusLabs.Needlr.Generators;

public interface IService { }
public interface IOtherService { }

[Provider]
public interface IMyProvider
{
    IService Service { get; }
    IOtherService OtherService { get; }
}
" + Attributes;

        var test = new CSharpAnalyzerTest<ProviderAttributeAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_WhenProviderInterfaceHasSettableProperty()
    {
        var code = @"
using NexusLabs.Needlr.Generators;

public interface IService { }

[Provider]
public interface IMyProvider
{
    {|#0:IService Service { get; set; }|}
}
" + Attributes;

        var test = new CSharpAnalyzerTest<ProviderAttributeAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics =
            {
                new DiagnosticResult("NDLRGEN032", Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                    .WithLocation(0)
                    .WithArguments("IMyProvider", "a settable property 'Service'")
            }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_WhenProviderInterfaceHasMethod()
    {
        var code = @"
using NexusLabs.Needlr.Generators;

public interface IService { }

[Provider]
public interface IMyProvider
{
    IService Service { get; }
    {|#0:void DoSomething();|}
}
" + Attributes;

        var test = new CSharpAnalyzerTest<ProviderAttributeAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics =
            {
                new DiagnosticResult("NDLRGEN032", Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                    .WithLocation(0)
                    .WithArguments("IMyProvider", "a method 'DoSomething'")
            }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Warning_WhenProviderPropertyIsConcreteClass()
    {
        var code = @"
using NexusLabs.Needlr.Generators;

public class ConcreteService { }

[Provider]
public interface IMyProvider
{
    {|#0:ConcreteService Service { get; }|}
}
" + Attributes;

        var test = new CSharpAnalyzerTest<ProviderAttributeAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics =
            {
                new DiagnosticResult("NDLRGEN033", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                    .WithLocation(0)
                    .WithArguments("IMyProvider", "Service", "ConcreteService")
            }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoWarning_WhenProviderPropertyIsInterface()
    {
        var code = @"
using NexusLabs.Needlr.Generators;

public interface IService { }

[Provider]
public interface IMyProvider
{
    IService Service { get; }
}
" + Attributes;

        var test = new CSharpAnalyzerTest<ProviderAttributeAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoWarning_WhenProviderPropertyIsFactory()
    {
        var code = @"
using NexusLabs.Needlr.Generators;

public interface IServiceFactory { }

[Provider]
public interface IMyProvider
{
    IServiceFactory ServiceFactory { get; }
}
" + Attributes;

        var test = new CSharpAnalyzerTest<ProviderAttributeAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoWarning_WhenProviderPropertyIsCollection()
    {
        var code = @"
using NexusLabs.Needlr.Generators;
using System.Collections.Generic;

public class ConcreteHandler { }

[Provider]
public interface IMyProvider
{
    IEnumerable<ConcreteHandler> Handlers { get; }
}
" + Attributes;

        var test = new CSharpAnalyzerTest<ProviderAttributeAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}
