using System.Threading.Tasks;

using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

using Xunit;

namespace NexusLabs.Needlr.AgentFramework.Analyzers.Tests;

public sealed class AgentGroupChatSingletonAnalyzerTests
{
    private static string Attributes => MafTestAttributes.All;

    [Fact]
    public async Task NoError_WhenGroupHasTwoOrMoreMembers()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
[AgentGroupChatMember(""code-review"")]
public class SecurityAgent { }

[NeedlrAiAgent]
[AgentGroupChatMember(""code-review"")]
public class PerformanceAgent { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGroupChatSingletonAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoError_WhenGroupHasThreeMembers()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
[AgentGroupChatMember(""code-review"")]
public class SecurityAgent { }

[NeedlrAiAgent]
[AgentGroupChatMember(""code-review"")]
public class PerformanceAgent { }

[NeedlrAiAgent]
[AgentGroupChatMember(""code-review"")]
public class StyleAgent { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGroupChatSingletonAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoError_WhenNoGroupChatMemberAttributes()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
public class SecurityAgent { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGroupChatSingletonAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_NDLRMAF002_WhenGroupHasOneMember()
    {
        // NDLRMAF002 is reported on the attribute application span
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
[{|NDLRMAF002:AgentGroupChatMember(""code-review"")|}]
public class SecurityAgent { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGroupChatSingletonAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_NDLRMAF002_TwoGroupsOneUnderPopulated()
    {
        // code-review is fine (2 members), design-review has only 1 → error on design-review attribute
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
[AgentGroupChatMember(""code-review"")]
public class SecurityAgent { }

[NeedlrAiAgent]
[AgentGroupChatMember(""code-review"")]
public class PerformanceAgent { }

[NeedlrAiAgent]
[{|NDLRMAF002:AgentGroupChatMember(""design-review"")|}]
public class DesignAgent { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentGroupChatSingletonAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}
