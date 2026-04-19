using System.Threading.Tasks;

using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

using Xunit;

namespace NexusLabs.Needlr.AgentFramework.Analyzers.Tests;

public sealed class ToolResultToStringAnalyzerTests
{
    private const string ToolCallResultStub = @"
namespace NexusLabs.Needlr.AgentFramework.Iterative
{
    public sealed class ToolCallResult
    {
        public string FunctionName { get; set; }
        public object? Result { get; set; }
        public System.TimeSpan Duration { get; set; }
        public bool Succeeded { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
";

    private const string FunctionResultContentStub = @"
namespace Microsoft.Extensions.AI
{
    public class FunctionResultContent
    {
        public string? CallId { get; set; }
        public object? Result { get; set; }
    }
}
";

    private const string Stubs = ToolCallResultStub + FunctionResultContentStub;

    [Fact]
    public async Task Warning_WhenToStringCalledOnToolCallResult()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework.Iterative;

public class MyClass
{
    public void Process(ToolCallResult result)
    {
        var text = {|#0:result.Result.ToString()|};
    }
}
" + Stubs;

        var test = new CSharpAnalyzerTest<ToolResultToStringAnalyzer, DefaultVerifier>
        {
            TestCode = code,
        };
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult(MafDiagnosticDescriptors.ToolResultToStringCall)
                .WithLocation(0)
                .WithArguments("NexusLabs.Needlr.AgentFramework.Iterative.ToolCallResult"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Warning_WhenToStringCalledOnFunctionResultContent()
    {
        var code = @"
using Microsoft.Extensions.AI;

public class MyClass
{
    public void Process(FunctionResultContent fr)
    {
        var text = {|#0:fr.Result.ToString()|};
    }
}
" + Stubs;

        var test = new CSharpAnalyzerTest<ToolResultToStringAnalyzer, DefaultVerifier>
        {
            TestCode = code,
        };
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult(MafDiagnosticDescriptors.ToolResultToStringCall)
                .WithLocation(0)
                .WithArguments("Microsoft.Extensions.AI.FunctionResultContent"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoWarning_WhenToStringCalledOnOtherProperty()
    {
        var code = @"
public class MyClass
{
    public object? Result { get; set; }

    public void Process()
    {
        var text = Result.ToString();
    }
}
" + Stubs;

        var test = new CSharpAnalyzerTest<ToolResultToStringAnalyzer, DefaultVerifier>
        {
            TestCode = code,
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoWarning_WhenAccessingResultWithoutToString()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework.Iterative;

public class MyClass
{
    public void Process(ToolCallResult result)
    {
        var obj = result.Result;
    }
}
" + Stubs;

        var test = new CSharpAnalyzerTest<ToolResultToStringAnalyzer, DefaultVerifier>
        {
            TestCode = code,
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoWarning_WhenToStringCalledOnOtherMember()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework.Iterative;

public class MyClass
{
    public void Process(ToolCallResult result)
    {
        var text = result.FunctionName.ToString();
    }
}
" + Stubs;

        var test = new CSharpAnalyzerTest<ToolResultToStringAnalyzer, DefaultVerifier>
        {
            TestCode = code,
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Warning_WhenNullConditionalToStringCalledOnToolCallResult()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework.Iterative;

public class MyClass
{
    public void Process(ToolCallResult result)
    {
        var text = result.Result?.ToString() ?? """";
    }
}
" + Stubs;

        var test = new CSharpAnalyzerTest<ToolResultToStringAnalyzer, DefaultVerifier>
        {
            TestCode = code,
        };
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult(MafDiagnosticDescriptors.ToolResultToStringCall)
                .WithSpan(8, 34, 8, 45)
                .WithArguments("NexusLabs.Needlr.AgentFramework.Iterative.ToolCallResult"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoWarning_WhenToStringCalledOnDuration()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework.Iterative;

public class MyClass
{
    public void Process(ToolCallResult result)
    {
        var text = result.Duration.ToString();
    }
}
" + Stubs;

        var test = new CSharpAnalyzerTest<ToolResultToStringAnalyzer, DefaultVerifier>
        {
            TestCode = code,
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}
