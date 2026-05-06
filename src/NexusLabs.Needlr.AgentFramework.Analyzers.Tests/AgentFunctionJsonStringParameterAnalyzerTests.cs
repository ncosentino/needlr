using System.Threading.Tasks;

using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

using Xunit;

namespace NexusLabs.Needlr.AgentFramework.Analyzers.Tests;

public sealed class AgentFunctionJsonStringParameterAnalyzerTests
{
    private static string Attributes => MafTestAttributes.All;

    [Fact]
    public async Task NoDiagnostic_WhenStringParameterHasNoJsonSignal()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;
using System.ComponentModel;

public class MyTool
{
    [AgentFunction]
    [Description(""Looks up an order"")]
    public string GetOrder([Description(""Order id"")] string orderId) => orderId;
}
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentFunctionJsonStringParameterAnalyzer, DefaultVerifier>
        {
            TestCode = code,
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Diagnostic_WhenParameterNameEndsWithJson()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;
using System.ComponentModel;

public class MyTool
{
    [AgentFunction]
    [Description(""Records findings"")]
    public string Record([Description(""Findings to record"")] string {|NDLRMAF030:findingsJson|}) => findingsJson;
}
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentFunctionJsonStringParameterAnalyzer, DefaultVerifier>
        {
            TestCode = code,
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Diagnostic_WhenParameterNameEndsWithUnderscoreJson()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;
using System.ComponentModel;

public class MyTool
{
    [AgentFunction]
    [Description(""Saves notes"")]
    public string Save([Description(""Notes payload"")] string {|NDLRMAF030:notes_json|}) => notes_json;
}
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentFunctionJsonStringParameterAnalyzer, DefaultVerifier>
        {
            TestCode = code,
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Diagnostic_WhenDescriptionMentionsJsonArray()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;
using System.ComponentModel;

public class MyTool
{
    [AgentFunction]
    [Description(""Records findings"")]
    public string Record([Description(""JSON array of findings"")] string {|NDLRMAF030:payload|}) => payload;
}
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentFunctionJsonStringParameterAnalyzer, DefaultVerifier>
        {
            TestCode = code,
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Diagnostic_WhenDescriptionMentionsJsonObject_CaseInsensitive()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;
using System.ComponentModel;

public class MyTool
{
    [AgentFunction]
    [Description(""Saves a json object describing the user"")]
    public string Save([Description(""json object payload"")] string {|NDLRMAF030:payload|}) => payload;
}
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentFunctionJsonStringParameterAnalyzer, DefaultVerifier>
        {
            TestCode = code,
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoDiagnostic_WhenJsonElementParameter_RegardlessOfNameOrDescription()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;
using System.ComponentModel;
using System.Text.Json;

public class MyTool
{
    [AgentFunction]
    [Description(""Records findings"")]
    public string Record([Description(""JSON array of findings"")] JsonElement findingsJson) => findingsJson.ToString();
}
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentFunctionJsonStringParameterAnalyzer, DefaultVerifier>
        {
            TestCode = code,
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoDiagnostic_WhenMethodHasNoAgentFunctionAttribute()
    {
        var code = @"
using System.ComponentModel;

public class MyTool
{
    public string Record([Description(""JSON array of findings"")] string findingsJson) => findingsJson;
}
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentFunctionJsonStringParameterAnalyzer, DefaultVerifier>
        {
            TestCode = code,
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoDiagnostic_WhenStringParameterIsCalledJsonInTheMiddleOfTheName()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;
using System.ComponentModel;

public class MyTool
{
    [AgentFunction]
    [Description(""Records data"")]
    public string Record([Description(""Just data"")] string jsonData) => jsonData;
}
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentFunctionJsonStringParameterAnalyzer, DefaultVerifier>
        {
            TestCode = code,
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Diagnostic_WhenBothNameAndDescriptionMatch_FiresOnce()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;
using System.ComponentModel;

public class MyTool
{
    [AgentFunction]
    [Description(""Records findings"")]
    public string Record([Description(""JSON array of items"")] string {|NDLRMAF030:findingsJson|}) => findingsJson;
}
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentFunctionJsonStringParameterAnalyzer, DefaultVerifier>
        {
            TestCode = code,
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Diagnostic_FiresOnlyOnJsonParameter_NotOnSiblingPlainParameters()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;
using System.ComponentModel;

public class MyTool
{
    [AgentFunction]
    [Description(""Records findings"")]
    public string Record(
        [Description(""Section identifier"")] string sectionId,
        [Description(""JSON array of findings"")] string {|NDLRMAF030:findingsJson|},
        [Description(""Reviewer initials"")] string reviewer)
        => findingsJson;
}
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentFunctionJsonStringParameterAnalyzer, DefaultVerifier>
        {
            TestCode = code,
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}
