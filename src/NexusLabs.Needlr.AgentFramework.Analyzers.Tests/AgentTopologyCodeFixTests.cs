using System.Threading.Tasks;

using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

using Xunit;

namespace NexusLabs.Needlr.AgentFramework.Analyzers.Tests;

public sealed class AgentTopologyCodeFixTests
{
    [Fact]
    public async Task Fix_NDLRMAF001_AddsNeedlrAiAgentToTargetType()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

public class GeographyAgent { }

[NeedlrAiAgent]
[{|NDLRMAF001:AgentHandoffsTo(typeof(GeographyAgent), ""geography questions"")|}]
public class TriageAgent { }
" + MafTestAttributes.All;

        var fixedCode = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
public class GeographyAgent { }

[NeedlrAiAgent]
[AgentHandoffsTo(typeof(GeographyAgent), ""geography questions"")]
public class TriageAgent { }
" + MafTestAttributes.All;

        var test = new CSharpCodeFixTest<AgentTopologyAnalyzer, AgentTopologyCodeFixProvider, DefaultVerifier>
        {
            TestCode = code,
            FixedCode = fixedCode
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Fix_NDLRMAF003_AddsNeedlrAiAgentToSourceClass()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
public class GeographyAgent { }

[AgentHandoffsTo(typeof(GeographyAgent), ""geography questions"")]
public class {|NDLRMAF003:TriageAgent|} { }
" + MafTestAttributes.All;

        var fixedCode = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
public class GeographyAgent { }

[NeedlrAiAgent]
[AgentHandoffsTo(typeof(GeographyAgent), ""geography questions"")]
public class TriageAgent { }
" + MafTestAttributes.All;

        var test = new CSharpCodeFixTest<AgentTopologyAnalyzer, AgentTopologyCodeFixProvider, DefaultVerifier>
        {
            TestCode = code,
            FixedCode = fixedCode
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}
