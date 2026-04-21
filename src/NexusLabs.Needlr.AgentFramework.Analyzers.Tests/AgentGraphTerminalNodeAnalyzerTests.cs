using System.Threading.Tasks;

using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

using Xunit;

namespace NexusLabs.Needlr.AgentFramework.Analyzers.Tests;

public sealed class AgentGraphTerminalNodeAnalyzerTests
{
    private static string Attributes => MafTestAttributes.All + MafTestAttributes.GraphAttributes;

    [Fact]
    public async Task NoDiagnostic_WhenTerminalNodeHasNoEdges()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
[AgentGraphNode(""Pipeline"", IsTerminal = true)]
public class TerminalAgent { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGraphTerminalNodeAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_NDLRMAF027_WhenTerminalNodeHasOutgoingEdges()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
[{|NDLRMAF027:AgentGraphNode(""Pipeline"", IsTerminal = true)|}]
[AgentGraphEdge(""Pipeline"", typeof(NextAgent))]
public class TerminalAgent { }

[NeedlrAiAgent]
public class NextAgent { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGraphTerminalNodeAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoDiagnostic_WhenNodeIsNotTerminal()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
[AgentGraphNode(""Pipeline"")]
[AgentGraphEdge(""Pipeline"", typeof(NextAgent))]
public class AgentA { }

[NeedlrAiAgent]
public class NextAgent { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGraphTerminalNodeAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoDiagnostic_WhenTerminalInDifferentGraph()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
[AgentGraphNode(""Graph1"", IsTerminal = true)]
[AgentGraphEdge(""Graph2"", typeof(NextAgent))]
public class AgentA { }

[NeedlrAiAgent]
public class NextAgent { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGraphTerminalNodeAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoDiagnostic_WhenNoGraphAttributes()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
public class AgentA { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGraphTerminalNodeAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}
