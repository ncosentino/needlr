using System.Threading.Tasks;

using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

using Xunit;

namespace NexusLabs.Needlr.AgentFramework.Analyzers.Tests;

public sealed class AgentGraphEntryPointAnalyzerTests
{
    private static string Attributes => MafTestAttributes.All + MafTestAttributes.GraphAttributes;

    [Fact]
    public async Task NoDiagnostic_WhenSingleEntryPoint()
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

        var test = new CSharpAnalyzerTest<AgentGraphEntryPointAnalyzer, DefaultVerifier>
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

        var test = new CSharpAnalyzerTest<AgentGraphEntryPointAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_NDLRMAF017_WhenEdgesButNoEntryPoint()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
[{|NDLRMAF017:AgentGraphEdge(""Pipeline"", typeof(AgentB))|}]
public class AgentA { }

[NeedlrAiAgent]
public class AgentB { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGraphEntryPointAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_NDLRMAF018_WhenMultipleEntryPoints()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
[{|NDLRMAF018:AgentGraphEntry(""Pipeline"")|}]
[AgentGraphEdge(""Pipeline"", typeof(AgentC))]
public class AgentA { }

[NeedlrAiAgent]
[{|NDLRMAF018:AgentGraphEntry(""Pipeline"")|}]
[AgentGraphEdge(""Pipeline"", typeof(AgentC))]
public class AgentB { }

[NeedlrAiAgent]
public class AgentC { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGraphEntryPointAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoDiagnostic_DifferentGraphNames_EachHasOneEntry()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
[AgentGraphEntry(""Graph1"")]
[AgentGraphEdge(""Graph1"", typeof(AgentC))]
public class AgentA { }

[NeedlrAiAgent]
[AgentGraphEntry(""Graph2"")]
[AgentGraphEdge(""Graph2"", typeof(AgentC))]
public class AgentB { }

[NeedlrAiAgent]
public class AgentC { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGraphEntryPointAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}
