using System.Threading.Tasks;

using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

using Xunit;

namespace NexusLabs.Needlr.AgentFramework.Analyzers.Tests;

public sealed class AgentFunctionDescriptionAnalyzerTests
{
    private static string Attributes => MafTestAttributes.All;

    [Fact]
    public async Task NoWarning_WhenAgentFunctionHasDescription()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;
using System.ComponentModel;

public class MyFunctions
{
    [AgentFunction]
    [Description(""Adds two numbers."")]
    public string Add(
        [Description(""First number."")] int a,
        [Description(""Second number."")] int b)
        => (a + b).ToString();
}
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentFunctionDescriptionAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoWarning_WhenMethodHasNoAgentFunctionAttribute()
    {
        var code = @"
public class MyFunctions
{
    public string Add(int a, int b) => (a + b).ToString();
}
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentFunctionDescriptionAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoWarning_WhenAgentFunctionHasNoCancellationTokenParam()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;
using System.ComponentModel;

public class MyFunctions
{
    [AgentFunction]
    [Description(""Does something."")]
    public string DoIt() => """";
}
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentFunctionDescriptionAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoWarning_ForCancellationTokenParameter()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;
using System.ComponentModel;
using System.Threading;

public class MyFunctions
{
    [AgentFunction]
    [Description(""Does something async."")]
    public System.Threading.Tasks.Task<string> DoItAsync(CancellationToken cancellationToken)
        => System.Threading.Tasks.Task.FromResult("""");
}
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentFunctionDescriptionAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Warning_NDLRMAF012_WhenAgentFunctionLacksDescription()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

public class MyFunctions
{
    [{|NDLRMAF012:AgentFunction|}]
    public string GetData() => """";
}
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentFunctionDescriptionAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Warning_NDLRMAF013_WhenParameterLacksDescription()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;
using System.ComponentModel;

public class MyFunctions
{
    [AgentFunction]
    [Description(""Adds two numbers."")]
    public string Add(int {|NDLRMAF013:a|}, int {|NDLRMAF013:b|}) => (a + b).ToString();
}
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentFunctionDescriptionAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Warning_NDLRMAF012_And_NDLRMAF013_WhenBothMissing()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

public class MyFunctions
{
    [{|NDLRMAF012:AgentFunction|}]
    public string Add(int {|NDLRMAF013:a|}, int {|NDLRMAF013:b|}) => (a + b).ToString();
}
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentFunctionDescriptionAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Warning_NDLRMAF013_OnlyForParamsWithoutDescription()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;
using System.ComponentModel;

public class MyFunctions
{
    [AgentFunction]
    [Description(""Searches for items."")]
    public string Search(
        [Description(""Search query."")] string query,
        int {|NDLRMAF013:maxResults|})
        => """";
}
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentFunctionDescriptionAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Warning_NDLRMAF012_PerMethod()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

public class MyFunctions
{
    [{|NDLRMAF012:AgentFunction|}]
    public string GetFoo() => """";

    [{|NDLRMAF012:AgentFunction|}]
    public string GetBar() => """";
}
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentFunctionDescriptionAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}
