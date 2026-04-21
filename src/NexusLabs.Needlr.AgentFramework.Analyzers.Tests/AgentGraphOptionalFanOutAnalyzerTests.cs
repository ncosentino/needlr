using System.Threading.Tasks;

using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

using Xunit;

namespace NexusLabs.Needlr.AgentFramework.Analyzers.Tests;

public sealed class AgentGraphOptionalFanOutAnalyzerTests
{
    private static string Attributes => MafTestAttributes.All + MafTestAttributes.GraphAttributes;

    [Fact]
    public async Task NoDiagnostic_WhenAtLeastOneEdgeIsRequired()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
[AgentGraphEdge(""Pipeline"", typeof(AgentB))]
[AgentGraphEdge(""Pipeline"", typeof(AgentC), IsRequired = false)]
public class AgentA { }

[NeedlrAiAgent]
public class AgentB { }

[NeedlrAiAgent]
public class AgentC { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGraphOptionalFanOutAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoDiagnostic_WhenSingleEdge()
    {
        // Single edge (not fan-out) should not trigger even if optional
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
[AgentGraphEdge(""Pipeline"", typeof(AgentB), IsRequired = false)]
public class AgentA { }

[NeedlrAiAgent]
public class AgentB { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGraphOptionalFanOutAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Warning_NDLRMAF024_WhenAllFanOutEdgesOptional()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
[AgentGraphEdge(""Pipeline"", typeof(AgentB), IsRequired = false)]
[AgentGraphEdge(""Pipeline"", typeof(AgentC), IsRequired = false)]
public class {|NDLRMAF024:AgentA|} { }

[NeedlrAiAgent]
public class AgentB { }

[NeedlrAiAgent]
public class AgentC { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGraphOptionalFanOutAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoDiagnostic_WhenAllEdgesRequired()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
[AgentGraphEdge(""Pipeline"", typeof(AgentB))]
[AgentGraphEdge(""Pipeline"", typeof(AgentC))]
public class AgentA { }

[NeedlrAiAgent]
public class AgentB { }

[NeedlrAiAgent]
public class AgentC { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGraphOptionalFanOutAnalyzer, DefaultVerifier>
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

        var test = new CSharpAnalyzerTest<AgentGraphOptionalFanOutAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}
