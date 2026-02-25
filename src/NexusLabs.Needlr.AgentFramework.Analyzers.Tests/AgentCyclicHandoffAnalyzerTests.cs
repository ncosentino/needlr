using System.Threading.Tasks;

using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

using Xunit;

namespace NexusLabs.Needlr.AgentFramework.Analyzers.Tests;

public sealed class AgentCyclicHandoffAnalyzerTests
{
    private static string Attributes => MafTestAttributes.All;

    [Fact]
    public async Task NoWarning_WhenNoCycleExists()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
public class GeographyAgent { }

[NeedlrAiAgent]
[AgentHandoffsTo(typeof(GeographyAgent))]
public class TriageAgent { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentCyclicHandoffAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoWarning_WhenNoHandoffAttributes()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
public class GeographyAgent { }

[NeedlrAiAgent]
public class TriageAgent { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentCyclicHandoffAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoWarning_LinearChain()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
public class AgentC { }

[NeedlrAiAgent]
[AgentHandoffsTo(typeof(AgentC))]
public class AgentB { }

[NeedlrAiAgent]
[AgentHandoffsTo(typeof(AgentB))]
public class AgentA { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentCyclicHandoffAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Warning_NDLRMAF004_TwoNodeCycle()
    {
        // NDLRMAF004 is reported on each attribute application in the cycle
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
[{|NDLRMAF004:AgentHandoffsTo(typeof(AgentB))|}]
public class AgentA { }

[NeedlrAiAgent]
[{|NDLRMAF004:AgentHandoffsTo(typeof(AgentA))|}]
public class AgentB { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentCyclicHandoffAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Warning_NDLRMAF004_ThreeNodeCycle()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
[{|NDLRMAF004:AgentHandoffsTo(typeof(AgentB))|}]
public class AgentA { }

[NeedlrAiAgent]
[{|NDLRMAF004:AgentHandoffsTo(typeof(AgentC))|}]
public class AgentB { }

[NeedlrAiAgent]
[{|NDLRMAF004:AgentHandoffsTo(typeof(AgentA))|}]
public class AgentC { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentCyclicHandoffAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}
