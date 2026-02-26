using System.Threading.Tasks;

using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

using Xunit;

namespace NexusLabs.Needlr.AgentFramework.Analyzers.Tests;

public sealed class TerminationConditionAnalyzerTests
{
    private static string Attributes => MafTestAttributes.All;

    // ─── NDLRMAF009 ──────────────────────────────────────────────────────────

    [Fact]
    public async Task NoWarning_009_WhenWorkflowRunConditionOnAgent()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
[AgentSequenceMember(""pipeline"", 0)]
[WorkflowRunTerminationCondition(typeof(MyCondition), ""DONE"")]
public class WriterAgent { }

public class MyCondition : IWorkflowTerminationCondition
{
    public bool ShouldTerminate(object context) => false;
}
" + Attributes;

        var test = new CSharpAnalyzerTest<TerminationConditionAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Warning_NDLRMAF009_WhenWorkflowRunConditionOnNonAgent()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[{|NDLRMAF009:WorkflowRunTerminationCondition(typeof(MyCondition), ""DONE"")|}]
public class NotAnAgent { }

public class MyCondition : IWorkflowTerminationCondition
{
    public bool ShouldTerminate(object context) => false;
}
" + Attributes;

        var test = new CSharpAnalyzerTest<TerminationConditionAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Warning_NDLRMAF009_MultipleConditions_AllFlagNonAgent()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[{|NDLRMAF009:WorkflowRunTerminationCondition(typeof(MyCondition), ""A"")|}]
[{|NDLRMAF009:WorkflowRunTerminationCondition(typeof(MyCondition), ""B"")|}]
public class NotAnAgent { }

public class MyCondition : IWorkflowTerminationCondition
{
    public bool ShouldTerminate(object context) => false;
}
" + Attributes;

        var test = new CSharpAnalyzerTest<TerminationConditionAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    // ─── NDLRMAF010 ──────────────────────────────────────────────────────────

    [Fact]
    public async Task NoError_010_WhenWorkflowRunConditionTypeImplementsInterface()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
[AgentSequenceMember(""pipeline"", 0)]
[WorkflowRunTerminationCondition(typeof(ValidCondition), ""DONE"")]
public class WriterAgent { }

public class ValidCondition : IWorkflowTerminationCondition
{
    public bool ShouldTerminate(object context) => false;
}
" + Attributes;

        var test = new CSharpAnalyzerTest<TerminationConditionAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_NDLRMAF010_WhenWorkflowRunConditionTypeDoesNotImplementInterface()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
[AgentSequenceMember(""pipeline"", 0)]
[{|NDLRMAF010:WorkflowRunTerminationCondition(typeof(NotACondition), ""DONE"")|}]
public class WriterAgent { }

public class NotACondition { }
" + Attributes;

        var test = new CSharpAnalyzerTest<TerminationConditionAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoError_010_WhenAgentTerminationConditionTypeImplementsInterface()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
[AgentGroupChatMember(""review"")]
[AgentTerminationCondition(typeof(ValidCondition), ""APPROVED"")]
public class ReviewerAgent { }

[NeedlrAiAgent]
[AgentGroupChatMember(""review"")]
public class WriterAgent { }

public class ValidCondition : IWorkflowTerminationCondition
{
    public bool ShouldTerminate(object context) => false;
}
" + Attributes;

        var test = new CSharpAnalyzerTest<TerminationConditionAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_NDLRMAF010_WhenAgentTerminationConditionTypeDoesNotImplementInterface()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
[AgentGroupChatMember(""review"")]
[{|NDLRMAF010:AgentTerminationCondition(typeof(NotACondition), ""APPROVED"")|}]
public class ReviewerAgent { }

[NeedlrAiAgent]
[AgentGroupChatMember(""review"")]
public class WriterAgent { }

public class NotACondition { }
" + Attributes;

        var test = new CSharpAnalyzerTest<TerminationConditionAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_NDLRMAF010_MultipleConditions_EachBadTypeFlagged()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
[AgentSequenceMember(""pipeline"", 0)]
[{|NDLRMAF010:WorkflowRunTerminationCondition(typeof(BadTypeA), ""X"")|}]
[{|NDLRMAF010:WorkflowRunTerminationCondition(typeof(BadTypeB), ""Y"")|}]
public class WriterAgent { }

public class BadTypeA { }
public class BadTypeB { }
" + Attributes;

        var test = new CSharpAnalyzerTest<TerminationConditionAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    // ─── NDLRMAF011 ──────────────────────────────────────────────────────────

    [Fact]
    public async Task NoInfo_011_WhenWorkflowRunConditionOnSequenceMember()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
[AgentSequenceMember(""pipeline"", 0)]
[WorkflowRunTerminationCondition(typeof(MyCondition), ""DONE"")]
public class WriterAgent { }

[NeedlrAiAgent]
[AgentSequenceMember(""pipeline"", 1)]
public class EditorAgent { }

public class MyCondition : IWorkflowTerminationCondition
{
    public bool ShouldTerminate(object context) => false;
}
" + Attributes;

        var test = new CSharpAnalyzerTest<TerminationConditionAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Info_NDLRMAF011_WhenWorkflowRunConditionOnGroupChatMember()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
[AgentGroupChatMember(""review"")]
[{|NDLRMAF011:WorkflowRunTerminationCondition(typeof(MyCondition), ""APPROVED"")|}]
public class ReviewerAgent { }

[NeedlrAiAgent]
[AgentGroupChatMember(""review"")]
public class WriterAgent { }

public class MyCondition : IWorkflowTerminationCondition
{
    public bool ShouldTerminate(object context) => false;
}
" + Attributes;

        var test = new CSharpAnalyzerTest<TerminationConditionAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoInfo_011_WhenAgentTerminationConditionOnGroupChatMember()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
[AgentGroupChatMember(""review"")]
[AgentTerminationCondition(typeof(MyCondition), ""APPROVED"")]
public class ReviewerAgent { }

[NeedlrAiAgent]
[AgentGroupChatMember(""review"")]
public class WriterAgent { }

public class MyCondition : IWorkflowTerminationCondition
{
    public bool ShouldTerminate(object context) => false;
}
" + Attributes;

        var test = new CSharpAnalyzerTest<TerminationConditionAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Info_NDLRMAF011_MultipleConditions_EachFlagged()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
[AgentGroupChatMember(""review"")]
[{|NDLRMAF011:WorkflowRunTerminationCondition(typeof(MyCondition), ""A"")|}]
[{|NDLRMAF011:WorkflowRunTerminationCondition(typeof(MyCondition), ""B"")|}]
public class ReviewerAgent { }

[NeedlrAiAgent]
[AgentGroupChatMember(""review"")]
public class WriterAgent { }

public class MyCondition : IWorkflowTerminationCondition
{
    public bool ShouldTerminate(object context) => false;
}
" + Attributes;

        var test = new CSharpAnalyzerTest<TerminationConditionAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    // ─── combinations ────────────────────────────────────────────────────────

    [Fact]
    public async Task MultipleIssues_009And010_WhenBothApply()
    {
        // Not an agent AND condition type is wrong → both 009 and 010 fire on the same attribute
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[WorkflowRunTerminationCondition(typeof(BadType), ""X"")]
public class NotAnAgent { }

public class BadType { }
" + Attributes;

        var test = new CSharpAnalyzerTest<TerminationConditionAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics =
            {
                DiagnosticResult.CompilerError("NDLRMAF010").WithSpan(4, 2, 4, 55).WithArguments("BadType", "NotAnAgent"),
                DiagnosticResult.CompilerWarning("NDLRMAF009").WithSpan(4, 2, 4, 55).WithArguments("NotAnAgent"),
            }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}
