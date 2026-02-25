using System.Threading.Tasks;

using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

using Xunit;

namespace NexusLabs.Needlr.AgentFramework.Analyzers.Tests;

public sealed class AgentOrphanAnalyzerTests
{
    private static string Attributes => MafTestAttributes.All;

    [Fact]
    public async Task NoInfo_WhenAgentIsHandoffSource()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
[AgentHandoffsTo(typeof(ExpertAgent))]
public class TriageAgent { }

[NeedlrAiAgent]
public class ExpertAgent { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentOrphanAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoInfo_WhenAgentIsHandoffTarget()
    {
        // ExpertAgent is referenced as a handoff target — should not be flagged
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
[AgentHandoffsTo(typeof(ExpertAgent))]
public class TriageAgent { }

[NeedlrAiAgent]
public class ExpertAgent { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentOrphanAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoInfo_WhenAgentIsGroupChatMember()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
[AgentGroupChatMember(""review"")]
public class SecurityAgent { }

[NeedlrAiAgent]
[AgentGroupChatMember(""review"")]
public class PerformanceAgent { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentOrphanAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoInfo_WhenAgentIsSequenceMember()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
[AgentSequenceMember(""pipeline"", 0)]
public class WriterAgent { }

[NeedlrAiAgent]
[AgentSequenceMember(""pipeline"", 1)]
public class EditorAgent { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentOrphanAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Info_NDLRMAF008_WhenAgentHasNoTopologyDeclaration()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[{|NDLRMAF008:NeedlrAiAgent|}]
public class OrphanAgent { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentOrphanAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Info_NDLRMAF008_ForEachOrphanAgent()
    {
        // Two orphan agents → two diagnostics
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[{|NDLRMAF008:NeedlrAiAgent|}]
public class OrphanA { }

[{|NDLRMAF008:NeedlrAiAgent|}]
public class OrphanB { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentOrphanAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Info_NDLRMAF008_OnlyForOrphan_NotForTopologyParticipants()
    {
        // TriageAgent and ExpertAgent participate in handoff; StandaloneAgent does not
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
[AgentHandoffsTo(typeof(ExpertAgent))]
public class TriageAgent { }

[NeedlrAiAgent]
public class ExpertAgent { }

[{|NDLRMAF008:NeedlrAiAgent|}]
public class StandaloneAgent { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentOrphanAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoInfo_WhenNoAgentsExist()
    {
        var code = @"
public class SomeClass { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentOrphanAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}
