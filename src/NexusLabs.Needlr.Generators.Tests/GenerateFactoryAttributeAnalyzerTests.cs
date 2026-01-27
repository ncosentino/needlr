using System.Threading.Tasks;

using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

using Xunit;

namespace NexusLabs.Needlr.Generators.Tests;

/// <summary>
/// Tests for GenerateFactoryAttributeAnalyzer diagnostics:
/// - NDLRGEN003: All parameters injectable
/// - NDLRGEN004: No injectable parameters
/// - NDLRGEN005: Type argument not implemented
/// </summary>
public sealed class GenerateFactoryAttributeAnalyzerTests
{
    private static string Attributes => NeedlrTestAttributes.AllWithFactory;

    #region NDLRGEN003: All params injectable

    [Fact]
    public async Task Warning_WhenAllParamsAreInjectable()
    {
        var code = @"
using NexusLabs.Needlr.Generators;

public interface IDependency1 { }
public interface IDependency2 { }

[{|#0:GenerateFactory|}]
public class MyService
{
    public MyService(IDependency1 dep1, IDependency2 dep2) { }
}
" + Attributes;

        var test = new CSharpAnalyzerTest<GenerateFactoryAttributeAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics =
            {
                new DiagnosticResult("NDLRGEN003", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                    .WithLocation(0)
                    .WithArguments("MyService")
            }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoWarning_WhenMixedParams()
    {
        var code = @"
using NexusLabs.Needlr.Generators;

public interface IDependency { }

[GenerateFactory]
public class MyService
{
    public MyService(IDependency dep, string connectionString) { }
}
" + Attributes;

        var test = new CSharpAnalyzerTest<GenerateFactoryAttributeAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    #endregion

    #region NDLRGEN004: No injectable params

    [Fact]
    public async Task Warning_WhenNoInjectableParams()
    {
        var code = @"
using NexusLabs.Needlr.Generators;

[{|#0:GenerateFactory|}]
public class MyService
{
    public MyService(string connectionString, int timeout) { }
}
" + Attributes;

        var test = new CSharpAnalyzerTest<GenerateFactoryAttributeAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics =
            {
                new DiagnosticResult("NDLRGEN004", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                    .WithLocation(0)
                    .WithArguments("MyService")
            }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Warning_WhenNoConstructorParams()
    {
        var code = @"
using NexusLabs.Needlr.Generators;

[{|#0:GenerateFactory|}]
public class MyService
{
    public MyService() { }
}
" + Attributes;

        var test = new CSharpAnalyzerTest<GenerateFactoryAttributeAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics =
            {
                new DiagnosticResult("NDLRGEN004", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                    .WithLocation(0)
                    .WithArguments("MyService")
            }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    #endregion

    #region NDLRGEN005: Type argument not implemented

    [Fact]
    public async Task Error_WhenTypeArgNotImplemented()
    {
        var code = @"
using NexusLabs.Needlr.Generators;

public interface IMyService { }
public interface IOtherService { }

[{|#0:GenerateFactory<IOtherService>|}]
public class MyService : IMyService
{
    public MyService(IMyService dep, string config) { }
}
" + Attributes;

        var test = new CSharpAnalyzerTest<GenerateFactoryAttributeAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics =
            {
                new DiagnosticResult("NDLRGEN005", Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                    .WithLocation(0)
                    .WithArguments("MyService", "IOtherService")
            }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoError_WhenTypeArgIsImplemented()
    {
        var code = @"
using NexusLabs.Needlr.Generators;

public interface IMyService { }
public interface IDependency { }

[GenerateFactory<IMyService>]
public class MyService : IMyService
{
    public MyService(IDependency dep, string config) { }
}
" + Attributes;

        var test = new CSharpAnalyzerTest<GenerateFactoryAttributeAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoError_WhenTypeArgIsBaseInterface()
    {
        var code = @"
using NexusLabs.Needlr.Generators;

public interface IBaseService { }
public interface IMyService : IBaseService { }
public interface IDependency { }

[GenerateFactory<IBaseService>]
public class MyService : IMyService
{
    public MyService(IDependency dep, string config) { }
}
" + Attributes;

        var test = new CSharpAnalyzerTest<GenerateFactoryAttributeAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    #endregion

    #region Non-generic attribute still works

    [Fact]
    public async Task NoError_NonGenericWithMixedParams()
    {
        var code = @"
using NexusLabs.Needlr.Generators;

public interface IDependency { }

[GenerateFactory]
public class MyService
{
    public MyService(IDependency dep, string connectionString, int timeout) { }
}
" + Attributes;

        var test = new CSharpAnalyzerTest<GenerateFactoryAttributeAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    #endregion
}
