using System.Threading.Tasks;

using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

using Xunit;

namespace NexusLabs.Needlr.AgentFramework.Analyzers.Tests;

public sealed class AgentSequenceOrderAnalyzerTests
{
    private static string Attributes => MafTestAttributes.All;

    [Fact]
    public async Task NoError_WhenSingleAgentInPipeline()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
[AgentSequenceMember(""pipeline"", 0)]
public class WriterAgent { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentSequenceOrderAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoError_WhenOrderIsContiguous()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
[AgentSequenceMember(""content-pipeline"", 0)]
public class WriterAgent { }

[NeedlrAiAgent]
[AgentSequenceMember(""content-pipeline"", 1)]
public class EditorAgent { }

[NeedlrAiAgent]
[AgentSequenceMember(""content-pipeline"", 2)]
public class PublisherAgent { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentSequenceOrderAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoError_WhenTwoPipelinesEachContiguous()
    {
        // Same order values in different pipelines are independent
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
[AgentSequenceMember(""pipeline-a"", 0)]
public class StepA1 { }

[NeedlrAiAgent]
[AgentSequenceMember(""pipeline-a"", 1)]
public class StepA2 { }

[NeedlrAiAgent]
[AgentSequenceMember(""pipeline-b"", 0)]
public class StepB1 { }

[NeedlrAiAgent]
[AgentSequenceMember(""pipeline-b"", 1)]
public class StepB2 { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentSequenceOrderAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_NDLRMAF006_WhenTwoAgentsHaveSameOrder()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
[{|NDLRMAF006:AgentSequenceMember(""pipeline"", 1)|}]
public class WriterAgent { }

[NeedlrAiAgent]
[{|NDLRMAF006:AgentSequenceMember(""pipeline"", 1)|}]
public class EditorAgent { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentSequenceOrderAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_NDLRMAF006_WhenThreeAgentsHaveSameOrder()
    {
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
[{|NDLRMAF006:AgentSequenceMember(""pipeline"", 0)|}]
public class AgentA { }

[NeedlrAiAgent]
[{|NDLRMAF006:AgentSequenceMember(""pipeline"", 0)|}]
public class AgentB { }

[NeedlrAiAgent]
[{|NDLRMAF006:AgentSequenceMember(""pipeline"", 0)|}]
public class AgentC { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentSequenceOrderAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_NDLRMAF006_OnlyForDuplicatePipeline_NotOtherPipeline()
    {
        // pipeline-a has duplicate order 1; pipeline-b is fine → only pipeline-a gets errors
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
[{|NDLRMAF006:AgentSequenceMember(""pipeline-a"", 1)|}]
public class StepA1 { }

[NeedlrAiAgent]
[{|NDLRMAF006:AgentSequenceMember(""pipeline-a"", 1)|}]
public class StepA2 { }

[NeedlrAiAgent]
[AgentSequenceMember(""pipeline-b"", 0)]
public class StepB1 { }

[NeedlrAiAgent]
[AgentSequenceMember(""pipeline-b"", 1)]
public class StepB2 { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentSequenceOrderAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Warning_NDLRMAF007_WhenGapInOrderSequence()
    {
        // Orders 0, 1, 3 — gap at 2
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
[{|NDLRMAF007:AgentSequenceMember(""pipeline"", 0)|}]
public class WriterAgent { }

[NeedlrAiAgent]
[{|NDLRMAF007:AgentSequenceMember(""pipeline"", 1)|}]
public class EditorAgent { }

[NeedlrAiAgent]
[{|NDLRMAF007:AgentSequenceMember(""pipeline"", 3)|}]
public class PublisherAgent { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentSequenceOrderAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Warning_NDLRMAF007_OnFirstGapOnly()
    {
        // Orders 0, 2, 5 — two gaps (1 and 3/4), only first gap reported
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
[{|NDLRMAF007:AgentSequenceMember(""pipeline"", 0)|}]
public class AgentA { }

[NeedlrAiAgent]
[{|NDLRMAF007:AgentSequenceMember(""pipeline"", 2)|}]
public class AgentB { }

[NeedlrAiAgent]
[{|NDLRMAF007:AgentSequenceMember(""pipeline"", 5)|}]
public class AgentC { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentSequenceOrderAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoWarning_NDLRMAF007_WhenDuplicatesExistInSamePipeline()
    {
        // Duplicate order → NDLRMAF006, gap check suppressed → no NDLRMAF007
        var code = @"
using NexusLabs.Needlr.AgentFramework;

[NeedlrAiAgent]
[{|NDLRMAF006:AgentSequenceMember(""pipeline"", 0)|}]
public class AgentA { }

[NeedlrAiAgent]
[{|NDLRMAF006:AgentSequenceMember(""pipeline"", 0)|}]
public class AgentB { }

[NeedlrAiAgent]
[AgentSequenceMember(""pipeline"", 5)]
public class AgentC { }
" + Attributes;

        var test = new CSharpAnalyzerTest<AgentSequenceOrderAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}
