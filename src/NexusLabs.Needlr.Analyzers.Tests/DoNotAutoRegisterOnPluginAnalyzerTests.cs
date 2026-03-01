using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

using Xunit;

namespace NexusLabs.Needlr.Analyzers.Tests;

public sealed class DoNotAutoRegisterOnPluginAnalyzerTests
{
    private const string NeedlrTypes = @"
namespace NexusLabs.Needlr
{
    public interface IServiceCollectionPlugin { }
    public interface IWebApplicationPlugin { }
    public interface IWebApplicationBuilderPlugin { }

    [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Interface)]
    public sealed class DoNotAutoRegisterAttribute : System.Attribute { }

    [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Interface)]
    public sealed class DoNotInjectAttribute : System.Attribute { }
}";

    [Fact]
    public async Task Warning_WhenDoNotAutoRegisterOnClass_ImplementingServiceCollectionPlugin()
    {
        var code = @"
using NexusLabs.Needlr;

[DoNotAutoRegister]
public class MyPlugin : IServiceCollectionPlugin { }
" + NeedlrTypes;

        var expected = new DiagnosticResult(DiagnosticDescriptors.DoNotAutoRegisterOnPluginClass)
            .WithLocation(4, 1)
            .WithArguments("MyPlugin");

        var test = new CSharpAnalyzerTest<DoNotAutoRegisterOnPluginAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics = { expected }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Warning_WhenDoNotAutoRegisterOnClass_ImplementingWebApplicationPlugin()
    {
        var code = @"
using NexusLabs.Needlr;

[DoNotAutoRegister]
public class MyPlugin : IWebApplicationPlugin { }
" + NeedlrTypes;

        var expected = new DiagnosticResult(DiagnosticDescriptors.DoNotAutoRegisterOnPluginClass)
            .WithLocation(4, 1)
            .WithArguments("MyPlugin");

        var test = new CSharpAnalyzerTest<DoNotAutoRegisterOnPluginAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics = { expected }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Warning_WhenDoNotAutoRegisterOnClass_ImplementingWebApplicationBuilderPlugin()
    {
        var code = @"
using NexusLabs.Needlr;

[DoNotAutoRegister]
public class MyPlugin : IWebApplicationBuilderPlugin { }
" + NeedlrTypes;

        var expected = new DiagnosticResult(DiagnosticDescriptors.DoNotAutoRegisterOnPluginClass)
            .WithLocation(4, 1)
            .WithArguments("MyPlugin");

        var test = new CSharpAnalyzerTest<DoNotAutoRegisterOnPluginAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics = { expected }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoWarning_WhenDoNotAutoRegisterOnClass_NotImplementingPluginInterface()
    {
        var code = @"
using NexusLabs.Needlr;

[DoNotAutoRegister]
public class MyService { }
" + NeedlrTypes;

        var test = new CSharpAnalyzerTest<DoNotAutoRegisterOnPluginAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoWarning_WhenDoNotAutoRegisterAbsent_OnPluginClass()
    {
        var code = @"
using NexusLabs.Needlr;

public class MyPlugin : IServiceCollectionPlugin { }
" + NeedlrTypes;

        var test = new CSharpAnalyzerTest<DoNotAutoRegisterOnPluginAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}
