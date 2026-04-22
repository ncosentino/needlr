using System.Threading.Tasks;

using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

using Xunit;

namespace NexusLabs.Needlr.AgentFramework.Analyzers.Tests;

public sealed class AgentGraphConditionMethodAnalyzerTests
{
    private static string Attributes => MafTestAttributes.All + MafTestAttributes.GraphAttributes;

    [Fact]
    public async Task NoDiagnostic_WhenConditionMethodExists()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
[AgentGraphEntry(""Pipeline"")]
[AgentGraphEdge(""Pipeline"", typeof(AgentB), Condition = ""ShouldRoute"")]
public class AgentA
{
    public static bool ShouldRoute(object? input) => true;
}

[NeedlrAiAgent]
public class AgentB { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGraphConditionMethodAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoDiagnostic_WhenNoCondition()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
[AgentGraphEntry(""Pipeline"")]
[AgentGraphEdge(""Pipeline"", typeof(AgentB))]
public class AgentA { }

[NeedlrAiAgent]
public class AgentB { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGraphConditionMethodAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_NDLRMAF028_WhenConditionMethodNotFound()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
[AgentGraphEntry(""Pipeline"")]
[{|NDLRMAF028:AgentGraphEdge(""Pipeline"", typeof(AgentB), Condition = ""NonexistentMethod"")|}]
public class AgentA { }

[NeedlrAiAgent]
public class AgentB { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGraphConditionMethodAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_NDLRMAF028_WhenConditionMethodNotStatic()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
[AgentGraphEntry(""Pipeline"")]
[{|NDLRMAF028:AgentGraphEdge(""Pipeline"", typeof(AgentB), Condition = ""ShouldRoute"")|}]
public class AgentA
{
    public bool ShouldRoute(object? input) => true;
}

[NeedlrAiAgent]
public class AgentB { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGraphConditionMethodAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_NDLRMAF028_WhenConditionMethodWrongReturnType()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
[AgentGraphEntry(""Pipeline"")]
[{|NDLRMAF028:AgentGraphEdge(""Pipeline"", typeof(AgentB), Condition = ""ShouldRoute"")|}]
public class AgentA
{
    public static string ShouldRoute(object? input) => ""yes"";
}

[NeedlrAiAgent]
public class AgentB { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGraphConditionMethodAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}
