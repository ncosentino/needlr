using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

using Xunit;

namespace NexusLabs.Needlr.SignalR.Analyzers.Tests;

public sealed class HubPathAttributeAnalyzerTests
{
    private const string HubPathAttributeDefinition = @"
namespace NexusLabs.Needlr.SignalR
{
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public sealed class HubPathAttribute : System.Attribute
    {
        public HubPathAttribute(string hubPath, System.Type hubType) { }
        public HubPathAttribute(string hubPath) { }
    }
}";

    [Fact]
    public async Task NoWarning_WhenHubPathIsStringLiteral()
    {
        var code = @"
using NexusLabs.Needlr.SignalR;

[HubPath(""/myhub"", typeof(object))]
public class MyHub { }
" + HubPathAttributeDefinition;

        var test = new CSharpAnalyzerTest<HubPathAttributeAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoWarning_WhenHubPathIsConstField()
    {
        var code = @"
using NexusLabs.Needlr.SignalR;

public static class HubPaths
{
    public const string MyHubPath = ""/myhub"";
}

[HubPath(HubPaths.MyHubPath, typeof(object))]
public class MyHub { }
" + HubPathAttributeDefinition;

        var test = new CSharpAnalyzerTest<HubPathAttributeAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Warning_WhenHubPathIsNonConstant()
    {
        var code = @"
using NexusLabs.Needlr.SignalR;

public static class HubPaths
{
    public static string MyHubPath = ""/myhub"";
}

[HubPath({|#0:HubPaths.MyHubPath|}, typeof(object))]
public class MyHub { }
" + HubPathAttributeDefinition;

        var expected = new DiagnosticResult(DiagnosticIds.HubPathMustBeConstant, DiagnosticSeverity.Warning)
            .WithLocation(0)
            .WithArguments("HubPaths.MyHubPath");

        // Also expect compiler error because attribute args must be constant
        var compilerError = DiagnosticResult.CompilerError("CS0182").WithLocation(0);

        var test = new CSharpAnalyzerTest<HubPathAttributeAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics = { expected, compilerError }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoWarning_WhenHubTypeIsTypeOf()
    {
        var code = @"
using NexusLabs.Needlr.SignalR;

public class MyHubClass { }

[HubPath(""/myhub"", typeof(MyHubClass))]
public class MyHub { }
" + HubPathAttributeDefinition;

        var test = new CSharpAnalyzerTest<HubPathAttributeAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoWarning_WhenOnlyHubPathProvided()
    {
        var code = @"
using NexusLabs.Needlr.SignalR;

[HubPath(""/myhub"")]
public class MyHub { }
" + HubPathAttributeDefinition;

        var test = new CSharpAnalyzerTest<HubPathAttributeAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoWarning_WhenNotNeedlrHubPathAttribute()
    {
        var code = @"
namespace OtherNamespace
{
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public sealed class HubPathAttribute : System.Attribute
    {
        public HubPathAttribute(string hubPath) { }
    }
}

[OtherNamespace.HubPath({|#0:SomeClass.NotConstant|})]
public class MyHub { }

public static class SomeClass
{
    public static string NotConstant = ""/test"";
}
";
        // The compiler will still error on non-constant, but our analyzer shouldn't fire
        // because it's not the Needlr namespace
        var compilerError = DiagnosticResult.CompilerError("CS0182").WithLocation(0);

        var test = new CSharpAnalyzerTest<HubPathAttributeAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics = { compilerError }
        };

        // Should not report our diagnostic - different namespace
        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoWarning_WhenUsingNamedArgumentsWithConstant()
    {
        var code = @"
using NexusLabs.Needlr.SignalR;

public class MyHubClass { }

[HubPath(hubPath: ""/myhub"", hubType: typeof(MyHubClass))]
public class MyHub { }
" + HubPathAttributeDefinition;

        var test = new CSharpAnalyzerTest<HubPathAttributeAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Warning_WhenUsingNamedArgumentsWithNonConstant()
    {
        var code = @"
using NexusLabs.Needlr.SignalR;

public static class Paths
{
    public static string Hub = ""/hub"";
}

public class MyHubClass { }

[HubPath(hubPath: {|#0:Paths.Hub|}, hubType: typeof(MyHubClass))]
public class MyHub { }
" + HubPathAttributeDefinition;

        var expected = new DiagnosticResult(DiagnosticIds.HubPathMustBeConstant, DiagnosticSeverity.Warning)
            .WithLocation(0)
            .WithArguments("Paths.Hub");

        // Also expect compiler error because attribute args must be constant
        var compilerError = DiagnosticResult.CompilerError("CS0182").WithLocation(0);

        var test = new CSharpAnalyzerTest<HubPathAttributeAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics = { expected, compilerError }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}
