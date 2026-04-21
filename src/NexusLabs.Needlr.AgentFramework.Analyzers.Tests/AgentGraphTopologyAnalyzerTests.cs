using System.Threading.Tasks;

using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

using Xunit;

namespace NexusLabs.Needlr.AgentFramework.Analyzers.Tests;

public sealed class AgentGraphTopologyAnalyzerTests
{
    private static string Attributes => MafTestAttributes.All + MafTestAttributes.GraphAttributes;

    [Fact]
    public async Task NoDiagnostic_WhenSourceAndTargetAreAgents()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
[AgentGraphEdge(""Pipeline"", typeof(AgentB))]
public class AgentA { }

[NeedlrAiAgent]
public class AgentB { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGraphTopologyAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_NDLRMAF019_WhenTargetLacksNeedlrAiAgent()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
[{|NDLRMAF019:AgentGraphEdge(""Pipeline"", typeof(NotAnAgent))|}]
public class AgentA { }

public class NotAnAgent { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGraphTopologyAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Warning_NDLRMAF020_WhenSourceLacksNeedlrAiAgent()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[AgentGraphEdge(""Pipeline"", typeof(AgentB))]
public class {|NDLRMAF020:NotAnAgent|} { }

[NeedlrAiAgent]
public class AgentB { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGraphTopologyAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Warning_NDLRMAF021_WhenEntryPointLacksNeedlrAiAgent()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[AgentGraphEntry(""Pipeline"")]
public class {|NDLRMAF021:NotAnAgent|} { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGraphTopologyAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoDiagnostic_WhenEntryPointHasNeedlrAiAgent()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
[AgentGraphEntry(""Pipeline"")]
public class AgentA { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGraphTopologyAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task BothDiagnostics_WhenSourceAndTargetBothLackNeedlrAiAgent()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[{|NDLRMAF019:AgentGraphEdge(""Pipeline"", typeof(AlsoNotAnAgent))|}]
public class {|NDLRMAF020:NotAnAgent|} { }

public class AlsoNotAnAgent { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGraphTopologyAnalyzer, DefaultVerifier>
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

public class PlainClass { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGraphTopologyAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}
