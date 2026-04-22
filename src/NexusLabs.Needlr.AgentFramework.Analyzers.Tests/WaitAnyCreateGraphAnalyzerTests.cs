using System.Threading.Tasks;

using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

using Xunit;

namespace NexusLabs.Needlr.AgentFramework.Analyzers.Tests;

public sealed class WaitAnyCreateGraphAnalyzerTests
{
    private static string Attributes => MafTestAttributes.All + MafTestAttributes.GraphAttributes;

    private const string WorkflowFactoryStub = @"
namespace NexusLabs.Needlr.AgentFramework
{
    public interface IWorkflowFactory
    {
        object CreateGraphWorkflow(string graphName);
    }
}
";

    private static string AllStubs => Attributes + WorkflowFactoryStub;

    [Fact]
    public async Task Error_WhenCreateGraphWorkflowCalledOnWaitAnyGraph()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent(Instructions = ""Entry."")]
[AgentGraphEntry(""my-graph"")]
[AgentGraphEdge(""my-graph"", typeof(SinkAgent))]
public class EntryAgent { }

[NeedlrAiAgent(Instructions = ""Sink."")]
[AgentGraphNode(""my-graph"", JoinMode = GraphJoinMode.WaitAny)]
public class SinkAgent { }

public class Runner
{
    public void Run(IWorkflowFactory factory)
    {
        var workflow = {|#0:factory.CreateGraphWorkflow(""my-graph"")|};
    }
}
" + AllStubs;

        var expected = new DiagnosticResult(MafDiagnosticDescriptors.WaitAnyIncompatibleWithCreateGraphWorkflow)
            .WithLocation(0)
            .WithArguments("my-graph");

        await new CSharpAnalyzerTest<WaitAnyCreateGraphAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics = { expected },
        }.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoDiagnostic_WhenCreateGraphWorkflowCalledOnWaitAllGraph()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent(Instructions = ""Entry."")]
[AgentGraphEntry(""my-graph"")]
[AgentGraphEdge(""my-graph"", typeof(SinkAgent))]
public class EntryAgent { }

[NeedlrAiAgent(Instructions = ""Sink."")]
[AgentGraphNode(""my-graph"", JoinMode = GraphJoinMode.WaitAll)]
public class SinkAgent { }

public class Runner
{
    public void Run(IWorkflowFactory factory)
    {
        var workflow = factory.CreateGraphWorkflow(""my-graph"");
    }
}
" + AllStubs;

        await new CSharpAnalyzerTest<WaitAnyCreateGraphAnalyzer, DefaultVerifier>
        {
            TestCode = code,
        }.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoDiagnostic_WhenNoGraphNodeAttribute()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent(Instructions = ""Entry."")]
[AgentGraphEntry(""my-graph"")]
[AgentGraphEdge(""my-graph"", typeof(SinkAgent))]
public class EntryAgent { }

[NeedlrAiAgent(Instructions = ""Sink."")]
public class SinkAgent { }

public class Runner
{
    public void Run(IWorkflowFactory factory)
    {
        var workflow = factory.CreateGraphWorkflow(""my-graph"");
    }
}
" + AllStubs;

        await new CSharpAnalyzerTest<WaitAnyCreateGraphAnalyzer, DefaultVerifier>
        {
            TestCode = code,
        }.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoDiagnostic_WhenDifferentGraphName()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent(Instructions = ""Entry A."")]
[AgentGraphEntry(""graph-a"")]
[AgentGraphEdge(""graph-a"", typeof(SinkAgentA))]
public class EntryAgentA { }

[NeedlrAiAgent(Instructions = ""Sink A."")]
public class SinkAgentA { }

[NeedlrAiAgent(Instructions = ""Entry B."")]
[AgentGraphEntry(""graph-b"")]
[AgentGraphEdge(""graph-b"", typeof(SinkAgentB))]
public class EntryAgentB { }

[NeedlrAiAgent(Instructions = ""Sink B with WaitAny."")]
[AgentGraphNode(""graph-b"", JoinMode = GraphJoinMode.WaitAny)]
public class SinkAgentB { }

public class Runner
{
    public void Run(IWorkflowFactory factory)
    {
        // Should NOT fire - graph-a has no WaitAny nodes
        var workflow = factory.CreateGraphWorkflow(""graph-a"");
    }
}
" + AllStubs;

        await new CSharpAnalyzerTest<WaitAnyCreateGraphAnalyzer, DefaultVerifier>
        {
            TestCode = code,
        }.RunAsync(TestContext.Current.CancellationToken);
    }
}
