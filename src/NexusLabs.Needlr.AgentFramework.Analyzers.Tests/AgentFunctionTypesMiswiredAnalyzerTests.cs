using System.Threading.Tasks;

using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

using Xunit;

namespace NexusLabs.Needlr.AgentFramework.Analyzers.Tests;

public sealed class AgentFunctionTypesMiswiredAnalyzerTests
{
    private static string Attributes => MafTestAttributes.All;

    [Fact]
    public async Task NoWarning_WhenFunctionTypeHasAgentFunctionMethod()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

public class MyFunctions
{
    [AgentFunction]
    public string GetData() => """";
}

[NeedlrAiAgent(FunctionTypes = new[] { typeof(MyFunctions) })]
public class MyAgent { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentFunctionTypesMiswiredAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoWarning_WhenNoFunctionTypesSpecified()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
public class MyAgent { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentFunctionTypesMiswiredAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoWarning_WhenFunctionTypesIsEmpty()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent(FunctionTypes = new System.Type[0])]
public class MyAgent { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentFunctionTypesMiswiredAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Warning_NDLRMAF014_WhenFunctionTypeHasNoAgentFunctionMethods()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

public class EmptyFunctions
{
    public string NotAnAgentFunction() => """";
}

[{|NDLRMAF014:NeedlrAiAgent(FunctionTypes = new[] { typeof(EmptyFunctions) })|}]
public class MyAgent { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentFunctionTypesMiswiredAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Warning_NDLRMAF014_ForEachMiswiredType()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

public class EmptyFunctionsA
{
    public string NotAnAgentFunction() => """";
}

public class EmptyFunctionsB { }

[{|NDLRMAF014:NeedlrAiAgent(FunctionTypes = new[] { typeof(EmptyFunctionsA) })|}]
public class AgentA { }

[{|NDLRMAF014:NeedlrAiAgent(FunctionTypes = new[] { typeof(EmptyFunctionsB) })|}]
public class AgentB { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentFunctionTypesMiswiredAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Warning_NDLRMAF014_OnlyForTypesWithoutAgentFunctions()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

public class GoodFunctions
{
    [AgentFunction]
    public string DoWork() => """";
}

public class EmptyFunctions { }

[{|NDLRMAF014:NeedlrAiAgent(FunctionTypes = new[] { typeof(GoodFunctions), typeof(EmptyFunctions) })|}]
public class MyAgent { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentFunctionTypesMiswiredAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}
