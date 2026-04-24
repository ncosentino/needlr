using System.Threading.Tasks;

using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

using Xunit;

namespace NexusLabs.Needlr.AgentFramework.Analyzers.Tests;

public sealed class AgentGraphCycleAnalyzerTests
{
    private static string Attributes => MafTestAttributes.All + MafTestAttributes.GraphAttributes;

    [Fact]
    public async Task NoDiagnostic_WhenNoCycleExists()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
[AgentGraphEntry(""Pipeline"")]
[AgentGraphEdge(""Pipeline"", typeof(AgentB))]
public class AgentA { }

[NeedlrAiAgent]
[AgentGraphEdge(""Pipeline"", typeof(AgentC))]
public class AgentB { }

[NeedlrAiAgent]
public class AgentC { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGraphCycleAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoDiagnostic_WhenNoGraphEdges()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
public class AgentA { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGraphCycleAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_NDLRMAF016_TwoNodeCycle()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
[{|NDLRMAF016:AgentGraphEdge(""Pipeline"", typeof(AgentB))|}]
public class AgentA { }

[NeedlrAiAgent]
[{|NDLRMAF016:AgentGraphEdge(""Pipeline"", typeof(AgentA))|}]
public class AgentB { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGraphCycleAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_NDLRMAF016_ThreeNodeCycle()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
[{|NDLRMAF016:AgentGraphEdge(""Pipeline"", typeof(AgentB))|}]
public class AgentA { }

[NeedlrAiAgent]
[{|NDLRMAF016:AgentGraphEdge(""Pipeline"", typeof(AgentC))|}]
public class AgentB { }

[NeedlrAiAgent]
[{|NDLRMAF016:AgentGraphEdge(""Pipeline"", typeof(AgentA))|}]
public class AgentC { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGraphCycleAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoDiagnostic_CycleInDifferentGraph()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
[AgentGraphEdge(""Graph1"", typeof(AgentB))]
public class AgentA { }

[NeedlrAiAgent]
[AgentGraphEdge(""Graph2"", typeof(AgentA))]
public class AgentB { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGraphCycleAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_NDLRMAF016_SelfLoop()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
[{|NDLRMAF016:AgentGraphEdge(""Pipeline"", typeof(SelfLoopAgent))|}]
public class SelfLoopAgent { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGraphCycleAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoDiagnostic_DiamondDag()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
[AgentGraphEdge(""Pipeline"", typeof(AgentB))]
[AgentGraphEdge(""Pipeline"", typeof(AgentC))]
public class AgentA { }

[NeedlrAiAgent]
[AgentGraphEdge(""Pipeline"", typeof(AgentD))]
public class AgentB { }

[NeedlrAiAgent]
[AgentGraphEdge(""Pipeline"", typeof(AgentD))]
public class AgentC { }

[NeedlrAiAgent]
public class AgentD { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGraphCycleAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}
