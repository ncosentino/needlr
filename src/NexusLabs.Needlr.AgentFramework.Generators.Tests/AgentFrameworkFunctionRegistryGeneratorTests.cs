using Xunit;

namespace NexusLabs.Needlr.AgentFramework.Generators.Tests;

/// <summary>
/// Tests for <see cref="AgentFrameworkFunctionRegistryGenerator"/> covering all pipelines:
/// AgentFunction, AgentFunctionGroup, NeedlrAiAgent,
/// AgentHandoffsTo, AgentGroupChatMember, AgentSequenceMember, WorkflowRunTerminationCondition,
/// plus the bootstrap and workflow extension outputs.
/// </summary>
public sealed class AgentFrameworkFunctionRegistryGeneratorTests
{
    // -------------------------------------------------------------------------
    // Always-on: bootstrap ModuleInitializer
    // -------------------------------------------------------------------------

    [Fact]
    public void Generator_AlwaysEmits_ModuleInitializer_Bootstrap()
    {
        var output = MafGeneratorTestRunner.Create()
            .GetFile("NeedlrAgentFrameworkBootstrap.g.cs");

        Assert.Contains("ModuleInitializer", output);
        Assert.Contains("AgentFrameworkGeneratedBootstrap.Register", output);
    }

    [Fact]
    public void Generator_Bootstrap_PassesAllSixRegistries()
    {
        var output = MafGeneratorTestRunner.Create()
            .GetFile("NeedlrAgentFrameworkBootstrap.g.cs");

        Assert.Contains("AgentFrameworkFunctionRegistry.AllFunctionTypes", output);
        Assert.Contains("AgentFrameworkFunctionGroupRegistry.AllGroups", output);
        Assert.Contains("AgentRegistry.AllAgentTypes", output);
        Assert.Contains("AgentHandoffTopologyRegistry.AllHandoffs", output);
        Assert.Contains("AgentGroupChatRegistry.AllGroups", output);
        Assert.Contains("AgentSequentialTopologyRegistry.AllPipelines", output);
    }

    // -------------------------------------------------------------------------
    // Pipeline A — AgentFunction
    // -------------------------------------------------------------------------

    [Fact]
    public void PipelineA_ClassWithAgentFunctionMethod_EmittedInFunctionRegistry()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                public class CalculatorFunctions
                {
                    [NexusLabs.Needlr.AgentFramework.AgentFunction]
                    public string Add(int a, int b) => (a + b).ToString();
                }
            }
            """;

        var output = new MafGeneratorTestRunner()
            .WithSource(source)
            .GetFile("AgentFrameworkFunctions.g.cs");

        Assert.Contains("CalculatorFunctions", output);
        Assert.Contains("AllFunctionTypes", output);
    }

    [Fact]
    public void PipelineA_ClassWithoutAgentFunctionMethod_NotEmitted()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                public class PlainClass
                {
                    public void DoSomething() { }
                }
            }
            """;

        var output = new MafGeneratorTestRunner()
            .WithSource(source)
            .GetFile("AgentFrameworkFunctions.g.cs");

        Assert.DoesNotContain("PlainClass", output);
    }

    // -------------------------------------------------------------------------
    // Pipeline B — AgentFunctionGroup
    // -------------------------------------------------------------------------

    [Fact]
    public void PipelineB_AgentFunctionGroup_EmittedInGroupRegistry()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                [NexusLabs.Needlr.AgentFramework.AgentFunctionGroup("math-tools")]
                public class MathFunctions { }
            }
            """;

        var output = new MafGeneratorTestRunner()
            .WithSource(source)
            .GetFile("AgentFrameworkFunctionGroups.g.cs");

        Assert.Contains("math-tools", output);
        Assert.Contains("MathFunctions", output);
    }

    [Fact]
    public void PipelineB_MultipleTypesInSameGroup_BothEmitted()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                [NexusLabs.Needlr.AgentFramework.AgentFunctionGroup("shared-tools")]
                public class FunctionsA { }

                [NexusLabs.Needlr.AgentFramework.AgentFunctionGroup("shared-tools")]
                public class FunctionsB { }
            }
            """;

        var output = new MafGeneratorTestRunner()
            .WithSource(source)
            .GetFile("AgentFrameworkFunctionGroups.g.cs");

        Assert.Contains("FunctionsA", output);
        Assert.Contains("FunctionsB", output);
        // Only one group key
        Assert.Contains("\"shared-tools\"", output);
    }

    // -------------------------------------------------------------------------
    // Pipeline C — NeedlrAiAgent
    // -------------------------------------------------------------------------

    [Fact]
    public void PipelineC_NeedlrAiAgent_EmittedInAgentRegistry()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent(Instructions = "Triage issues.")]
                public class TriageAgent { }
            }
            """;

        var output = new MafGeneratorTestRunner()
            .WithSource(source)
            .GetFile("AgentRegistry.g.cs");

        Assert.Contains("TriageAgent", output);
        Assert.Contains("AllAgentTypes", output);
    }

    [Fact]
    public void PipelineC_MultipleAgents_AllEmitted()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                public class AgentOne { }

                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                public class AgentTwo { }
            }
            """;

        var output = new MafGeneratorTestRunner()
            .WithSource(source)
            .GetFile("AgentRegistry.g.cs");

        Assert.Contains("AgentOne", output);
        Assert.Contains("AgentTwo", output);
    }

    [Fact]
    public void PipelineC_PartialNeedlrAiAgent_EmitsPartialCompanion()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                public partial class ReviewAgent { }
            }
            """;

        var files = new MafGeneratorTestRunner()
            .WithSource(source)
            .RunGenerator();

        var companion = files.FirstOrDefault(f =>
            f.FilePath.Contains("ReviewAgent") && f.FilePath.EndsWith(".g.cs"));

        Assert.NotNull(companion);
        Assert.Contains("AgentName", companion.Content);
        Assert.Contains("nameof(ReviewAgent)", companion.Content);
    }

    [Fact]
    public void PipelineC_NonPartialNeedlrAiAgent_NoPartialCompanion()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                public class SolidAgent { }
            }
            """;

        var files = new MafGeneratorTestRunner()
            .WithSource(source)
            .RunGenerator();

        var companion = files.FirstOrDefault(f =>
            f.FilePath.Contains("SolidAgent") && !f.FilePath.Contains("AgentRegistry"));

        Assert.Null(companion);
    }

    [Fact]
    public void PipelineC_PartialAgent_NoToolDeclarations_UsesAllRegisteredToolsDoc()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                public partial class OrchestratorAgent { }
            }
            """;

        var files = new MafGeneratorTestRunner()
            .WithSource(source)
            .RunGenerator();

        var companion = files.First(f => f.FilePath.Contains("OrchestratorAgent") && f.FilePath.EndsWith(".g.cs"));
        Assert.Contains("all registered function types", companion.Content);
    }

    [Fact]
    public void PipelineC_PartialAgent_EmptyFunctionTypes_NoToolsDoc()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent(FunctionTypes = new System.Type[0])]
                public partial class TriageAgent { }
            }
            """;

        var files = new MafGeneratorTestRunner()
            .WithSource(source)
            .RunGenerator();

        var companion = files.First(f => f.FilePath.Contains("TriageAgent") && f.FilePath.EndsWith(".g.cs"));
        Assert.Contains("no tools assigned", companion.Content);
    }

    [Fact]
    public void PipelineC_PartialAgent_FunctionGroupDeclared_ResolvesGroupToTypeInDoc()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                [NexusLabs.Needlr.AgentFramework.AgentFunctionGroup("math-tools")]
                public class MathFunctions { }

                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent(FunctionGroups = new[] { "math-tools" })]
                public partial class MathAgent { }
            }
            """;

        var files = new MafGeneratorTestRunner()
            .WithSource(source)
            .RunGenerator();

        var companion = files.First(f => f.FilePath.Contains("MathAgent") && f.FilePath.EndsWith(".g.cs"));
        Assert.Contains("MathFunctions", companion.Content);
        Assert.Contains("math-tools", companion.Content);
    }

    [Fact]
    public void PipelineC_PartialAgent_FunctionGroupUnresolved_ShowsUnresolvedInDoc()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent(FunctionGroups = new[] { "missing-group" })]
                public partial class BrokenAgent { }
            }
            """;

        var files = new MafGeneratorTestRunner()
            .WithSource(source)
            .RunGenerator();

        var companion = files.First(f => f.FilePath.Contains("BrokenAgent") && f.FilePath.EndsWith(".g.cs"));
        Assert.Contains("unresolved group", companion.Content);
        Assert.Contains("missing-group", companion.Content);
    }

    [Fact]
    public void PipelineC_PartialAgent_ExplicitFunctionTypes_ShowsTypesInDoc()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                public class WritingFunctions { }

                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent(FunctionTypes = new[] { typeof(WritingFunctions) })]
                public partial class WriterAgent { }
            }
            """;

        var files = new MafGeneratorTestRunner()
            .WithSource(source)
            .RunGenerator();

        var companion = files.First(f => f.FilePath.Contains("WriterAgent") && f.FilePath.EndsWith(".g.cs"));
        Assert.Contains("WritingFunctions", companion.Content);
        Assert.Contains("explicit type", companion.Content);
    }

    // -------------------------------------------------------------------------
    // Pipeline D — AgentHandoffsTo
    // -------------------------------------------------------------------------

    [Fact]
    public void PipelineD_AgentHandoffsTo_EmittedInHandoffRegistry()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                public class SpecialistAgent { }

                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                [NexusLabs.Needlr.AgentFramework.AgentHandoffsTo(typeof(SpecialistAgent))]
                public class TriageAgent { }
            }
            """;

        var output = new MafGeneratorTestRunner()
            .WithSource(source)
            .GetFile("AgentHandoffTopologyRegistry.g.cs");

        Assert.Contains("TriageAgent", output);
        Assert.Contains("SpecialistAgent", output);
        Assert.Contains("AllHandoffs", output);
    }

    [Fact]
    public void PipelineD_HandoffWithReason_ReasonEmittedInRegistry()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                public class BillingAgent { }

                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                [NexusLabs.Needlr.AgentFramework.AgentHandoffsTo(typeof(BillingAgent), "billing query")]
                public class FrontlineAgent { }
            }
            """;

        var output = new MafGeneratorTestRunner()
            .WithSource(source)
            .GetFile("AgentHandoffTopologyRegistry.g.cs");

        Assert.Contains("billing query", output);
    }

    [Fact]
    public void PipelineD_AgentHandoffsTo_GeneratesHandoffExtensionMethod()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                public class SpecialistAgent { }

                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                [NexusLabs.Needlr.AgentFramework.AgentHandoffsTo(typeof(SpecialistAgent))]
                public class TriageAgent { }
            }
            """;

        var output = new MafGeneratorTestRunner()
            .WithSource(source)
            .GetFile("WorkflowFactoryExtensions.g.cs");

        Assert.Contains("CreateTriageHandoffWorkflow", output);
    }

    [Fact]
    public void PipelineD_ExtensionMethod_DocCommentListsHandoffTarget()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                public class ReviewAgent { }

                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                [NexusLabs.Needlr.AgentFramework.AgentHandoffsTo(typeof(ReviewAgent))]
                public class DispatchAgent { }
            }
            """;

        var output = new MafGeneratorTestRunner()
            .WithSource(source)
            .GetFile("WorkflowFactoryExtensions.g.cs");

        // Doc comment should reference the target
        Assert.Contains("ReviewAgent", output);
        Assert.Contains("<list type=\"bullet\">", output);
    }

    // -------------------------------------------------------------------------
    // Pipeline E — AgentGroupChatMember
    // -------------------------------------------------------------------------

    [Fact]
    public void PipelineE_AgentGroupChatMember_EmittedInGroupChatRegistry()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                [NexusLabs.Needlr.AgentFramework.AgentGroupChatMember("code-review")]
                public class ReviewerAgent { }

                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                [NexusLabs.Needlr.AgentFramework.AgentGroupChatMember("code-review")]
                public class CriticAgent { }
            }
            """;

        var output = new MafGeneratorTestRunner()
            .WithSource(source)
            .GetFile("AgentGroupChatRegistry.g.cs");

        Assert.Contains("\"code-review\"", output);
        Assert.Contains("ReviewerAgent", output);
        Assert.Contains("CriticAgent", output);
    }

    [Fact]
    public void PipelineE_AgentGroupChatMember_GeneratesGroupChatExtensionMethod()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                [NexusLabs.Needlr.AgentFramework.AgentGroupChatMember("code-review")]
                public class ReviewerAgent { }

                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                [NexusLabs.Needlr.AgentFramework.AgentGroupChatMember("code-review")]
                public class CriticAgent { }
            }
            """;

        var output = new MafGeneratorTestRunner()
            .WithSource(source)
            .GetFile("WorkflowFactoryExtensions.g.cs");

        Assert.Contains("CreateCodeReviewGroupChatWorkflow", output);
    }

    [Fact]
    public void PipelineE_GroupChatExtensionMethod_HasMaxIterationsParameter()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                [NexusLabs.Needlr.AgentFramework.AgentGroupChatMember("brainstorm")]
                public class IdeaAgent { }

                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                [NexusLabs.Needlr.AgentFramework.AgentGroupChatMember("brainstorm")]
                public class CritiqueAgent { }
            }
            """;

        var output = new MafGeneratorTestRunner()
            .WithSource(source)
            .GetFile("WorkflowFactoryExtensions.g.cs");

        Assert.Contains("maxIterations", output);
    }

    // -------------------------------------------------------------------------
    // Pipeline F — AgentSequenceMember
    // -------------------------------------------------------------------------

    [Fact]
    public void PipelineF_AgentSequenceMember_EmittedInSequentialRegistry()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                [NexusLabs.Needlr.AgentFramework.AgentSequenceMember("content-pipeline", 1)]
                public class WriterAgent { }

                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                [NexusLabs.Needlr.AgentFramework.AgentSequenceMember("content-pipeline", 2)]
                public class EditorAgent { }
            }
            """;

        var output = new MafGeneratorTestRunner()
            .WithSource(source)
            .GetFile("AgentSequentialTopologyRegistry.g.cs");

        Assert.Contains("\"content-pipeline\"", output);
        Assert.Contains("WriterAgent", output);
        Assert.Contains("EditorAgent", output);
        Assert.Contains("AllPipelines", output);
    }

    [Fact]
    public void PipelineF_AgentSequenceMember_OrderRespected()
    {
        // Declare agents in reverse order in source — generator must sort by Order
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                [NexusLabs.Needlr.AgentFramework.AgentSequenceMember("pipeline", 3)]
                public class PublisherAgent { }

                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                [NexusLabs.Needlr.AgentFramework.AgentSequenceMember("pipeline", 1)]
                public class WriterAgent { }

                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                [NexusLabs.Needlr.AgentFramework.AgentSequenceMember("pipeline", 2)]
                public class EditorAgent { }
            }
            """;

        var output = new MafGeneratorTestRunner()
            .WithSource(source)
            .GetFile("AgentSequentialTopologyRegistry.g.cs");

        var writerPos = output.IndexOf("WriterAgent", StringComparison.Ordinal);
        var editorPos = output.IndexOf("EditorAgent", StringComparison.Ordinal);
        var publisherPos = output.IndexOf("PublisherAgent", StringComparison.Ordinal);

        Assert.True(writerPos < editorPos, "WriterAgent (order 1) must appear before EditorAgent (order 2)");
        Assert.True(editorPos < publisherPos, "EditorAgent (order 2) must appear before PublisherAgent (order 3)");
    }

    [Fact]
    public void PipelineF_AgentSequenceMember_GeneratesSequentialExtensionMethod()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                [NexusLabs.Needlr.AgentFramework.AgentSequenceMember("content-pipeline", 1)]
                public class WriterAgent { }

                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                [NexusLabs.Needlr.AgentFramework.AgentSequenceMember("content-pipeline", 2)]
                public class EditorAgent { }
            }
            """;

        var output = new MafGeneratorTestRunner()
            .WithSource(source)
            .GetFile("WorkflowFactoryExtensions.g.cs");

        Assert.Contains("CreateContentPipelineSequentialWorkflow", output);
    }

    [Fact]
    public void PipelineF_SequentialExtensionMethod_UsesOrderedDocList()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                [NexusLabs.Needlr.AgentFramework.AgentSequenceMember("my-pipeline", 1)]
                public class StepOneAgent { }

                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                [NexusLabs.Needlr.AgentFramework.AgentSequenceMember("my-pipeline", 2)]
                public class StepTwoAgent { }
            }
            """;

        var output = new MafGeneratorTestRunner()
            .WithSource(source)
            .GetFile("WorkflowFactoryExtensions.g.cs");

        // Sequential uses ordered list (not bullet)
        Assert.Contains("<list type=\"number\">", output);
    }

    [Fact]
    public void PipelineF_MultiplePipelines_EachGetsSeparateExtensionMethod()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                [NexusLabs.Needlr.AgentFramework.AgentSequenceMember("pipeline-alpha", 1)]
                public class AlphaOneAgent { }

                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                [NexusLabs.Needlr.AgentFramework.AgentSequenceMember("pipeline-alpha", 2)]
                public class AlphaTwoAgent { }

                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                [NexusLabs.Needlr.AgentFramework.AgentSequenceMember("pipeline-beta", 1)]
                public class BetaOneAgent { }

                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                [NexusLabs.Needlr.AgentFramework.AgentSequenceMember("pipeline-beta", 2)]
                public class BetaTwoAgent { }
            }
            """;

        var output = new MafGeneratorTestRunner()
            .WithSource(source)
            .GetFile("WorkflowFactoryExtensions.g.cs");

        Assert.Contains("CreatePipelineAlphaSequentialWorkflow", output);
        Assert.Contains("CreatePipelineBetaSequentialWorkflow", output);
    }

    // -------------------------------------------------------------------------
    // Emitted file structure
    // -------------------------------------------------------------------------

    [Fact]
    public void Generator_EmitsAllExpectedFiles()
    {
        var files = MafGeneratorTestRunner.Create().RunGenerator();

        var fileNames = files.Select(f => System.IO.Path.GetFileName(f.FilePath)).ToArray();
        Assert.Contains("AgentFrameworkFunctions.g.cs", fileNames);
        Assert.Contains("AgentFrameworkFunctionGroups.g.cs", fileNames);
        Assert.Contains("AgentRegistry.g.cs", fileNames);
        Assert.Contains("AgentHandoffTopologyRegistry.g.cs", fileNames);
        Assert.Contains("AgentGroupChatRegistry.g.cs", fileNames);
        Assert.Contains("AgentSequentialTopologyRegistry.g.cs", fileNames);
        Assert.Contains("NeedlrAgentFrameworkBootstrap.g.cs", fileNames);
        Assert.Contains("WorkflowFactoryExtensions.g.cs", fileNames);
    }

    [Fact]
    public void Generator_EmptySource_AllRegistriesAreEmpty()
    {
        var agentOutput = MafGeneratorTestRunner.Create()
            .GetFile("AgentRegistry.g.cs");
        var handoffOutput = MafGeneratorTestRunner.Create()
            .GetFile("AgentHandoffTopologyRegistry.g.cs");
        var sequenceOutput = MafGeneratorTestRunner.Create()
            .GetFile("AgentSequentialTopologyRegistry.g.cs");

        // Empty arrays — no typeof(...) entries
        Assert.DoesNotContain("typeof(", agentOutput);
        Assert.DoesNotContain("typeof(", handoffOutput);
        Assert.DoesNotContain("typeof(", sequenceOutput);
    }

    [Fact]
    public void Generator_AllRegistries_HaveGeneratedCodeAttribute()
    {
        var agentOutput = MafGeneratorTestRunner.Create().GetFile("AgentRegistry.g.cs");

        Assert.Contains("[global::System.CodeDom.Compiler.GeneratedCodeAttribute", agentOutput);
    }

    // -------------------------------------------------------------------------
    // WorkflowRunTerminationCondition — Run*Async() generation
    // -------------------------------------------------------------------------

    [Fact]
    public void TerminationCondition_SequentialWorkflow_GeneratesRunAsyncMethod()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                [NexusLabs.Needlr.AgentFramework.AgentSequenceMember("review-pipeline", 1)]
                public class WriterAgent { }

                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                [NexusLabs.Needlr.AgentFramework.AgentSequenceMember("review-pipeline", 2)]
                [NexusLabs.Needlr.AgentFramework.WorkflowRunTerminationCondition(
                    typeof(NexusLabs.Needlr.AgentFramework.Workflows.KeywordTerminationCondition), "DONE")]
                public class EditorAgent { }
            }
            """;

        var output = new MafGeneratorTestRunner()
            .WithSource(source)
            .GetFile("WorkflowFactoryExtensions.g.cs");

        Assert.Contains("RunReviewPipelineSequentialWorkflowAsync", output);
        Assert.Contains("CreateReviewPipelineSequentialWorkflow", output);
    }

    [Fact]
    public void TerminationCondition_SequentialWorkflow_ConditionTypeAndArgEmitted()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                [NexusLabs.Needlr.AgentFramework.AgentSequenceMember("review-pipeline", 1)]
                [NexusLabs.Needlr.AgentFramework.WorkflowRunTerminationCondition(
                    typeof(NexusLabs.Needlr.AgentFramework.Workflows.KeywordTerminationCondition), "STATUS: DONE")]
                public class ReviewAgent { }
            }
            """;

        var output = new MafGeneratorTestRunner()
            .WithSource(source)
            .GetFile("WorkflowFactoryExtensions.g.cs");

        Assert.Contains("KeywordTerminationCondition", output);
        Assert.Contains("\"STATUS: DONE\"", output);
    }

    [Fact]
    public void TerminationCondition_SequentialWorkflow_MultipleConditions_AllEmitted()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                [NexusLabs.Needlr.AgentFramework.AgentSequenceMember("my-pipeline", 1)]
                [NexusLabs.Needlr.AgentFramework.WorkflowRunTerminationCondition(
                    typeof(NexusLabs.Needlr.AgentFramework.Workflows.KeywordTerminationCondition), "KEYWORD_STOP")]
                public class AgentA { }

                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                [NexusLabs.Needlr.AgentFramework.AgentSequenceMember("my-pipeline", 2)]
                [NexusLabs.Needlr.AgentFramework.WorkflowRunTerminationCondition(
                    typeof(NexusLabs.Needlr.AgentFramework.Workflows.RegexTerminationCondition), "ERROR:.*")]
                public class AgentB { }
            }
            """;

        var output = new MafGeneratorTestRunner()
            .WithSource(source)
            .GetFile("WorkflowFactoryExtensions.g.cs");

        Assert.Contains("KeywordTerminationCondition", output);
        Assert.Contains("\"KEYWORD_STOP\"", output);
        Assert.Contains("RegexTerminationCondition", output);
        Assert.Contains("\"ERROR:.*\"", output);
    }

    [Fact]
    public void TerminationCondition_HandoffWorkflow_GeneratesRunAsyncMethod()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                public class SpecialistAgent { }

                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                [NexusLabs.Needlr.AgentFramework.AgentHandoffsTo(typeof(SpecialistAgent))]
                [NexusLabs.Needlr.AgentFramework.WorkflowRunTerminationCondition(
                    typeof(NexusLabs.Needlr.AgentFramework.Workflows.KeywordTerminationCondition), "DONE")]
                public class RouterAgent { }
            }
            """;

        var output = new MafGeneratorTestRunner()
            .WithSource(source)
            .GetFile("WorkflowFactoryExtensions.g.cs");

        Assert.Contains("RunRouterHandoffWorkflowAsync", output);
        Assert.Contains("CreateRouterHandoffWorkflow", output);
    }

    [Fact]
    public void TerminationCondition_GroupChatWorkflow_GeneratesRunAsyncMethod()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                [NexusLabs.Needlr.AgentFramework.AgentGroupChatMember("review")]
                [NexusLabs.Needlr.AgentFramework.WorkflowRunTerminationCondition(
                    typeof(NexusLabs.Needlr.AgentFramework.Workflows.KeywordTerminationCondition), "APPROVED")]
                public class ReviewerAgent { }

                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                [NexusLabs.Needlr.AgentFramework.AgentGroupChatMember("review")]
                public class CriticAgent { }
            }
            """;

        var output = new MafGeneratorTestRunner()
            .WithSource(source)
            .GetFile("WorkflowFactoryExtensions.g.cs");

        Assert.Contains("RunReviewGroupChatWorkflowAsync", output);
        Assert.Contains("CreateReviewGroupChatWorkflow", output);
    }

    [Fact]
    public void TerminationCondition_NoConditionDeclared_NoRunAsyncMethodEmitted()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                [NexusLabs.Needlr.AgentFramework.AgentSequenceMember("plain-pipeline", 1)]
                public class AgentOne { }

                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                [NexusLabs.Needlr.AgentFramework.AgentSequenceMember("plain-pipeline", 2)]
                public class AgentTwo { }
            }
            """;

        var output = new MafGeneratorTestRunner()
            .WithSource(source)
            .GetFile("WorkflowFactoryExtensions.g.cs");

        Assert.Contains("CreatePlainPipelineSequentialWorkflow", output);
        Assert.DoesNotContain("RunPlainPipelineSequentialWorkflowAsync", output);
    }

    [Fact]
    public void TerminationCondition_RunAsyncMethod_UsesFullyQualifiedStreamingRunWorkflowExtensions()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                [NexusLabs.Needlr.AgentFramework.AgentSequenceMember("check-pipeline", 1)]
                [NexusLabs.Needlr.AgentFramework.WorkflowRunTerminationCondition(
                    typeof(NexusLabs.Needlr.AgentFramework.Workflows.KeywordTerminationCondition), "OK")]
                public class CheckAgent { }
            }
            """;

        var output = new MafGeneratorTestRunner()
            .WithSource(source)
            .GetFile("WorkflowFactoryExtensions.g.cs");

        Assert.Contains("global::NexusLabs.Needlr.AgentFramework.Workflows.StreamingRunWorkflowExtensions.RunAsync", output);
    }

    [Fact]
    public void TerminationCondition_RunAsyncMethod_ReturnsIReadOnlyDictionary()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                [NexusLabs.Needlr.AgentFramework.AgentSequenceMember("output-pipeline", 1)]
                [NexusLabs.Needlr.AgentFramework.WorkflowRunTerminationCondition(
                    typeof(NexusLabs.Needlr.AgentFramework.Workflows.KeywordTerminationCondition), "DONE")]
                public class OutputAgent { }
            }
            """;

        var output = new MafGeneratorTestRunner()
            .WithSource(source)
            .GetFile("WorkflowFactoryExtensions.g.cs");

        Assert.Contains("IReadOnlyDictionary<string, string>", output);
    }

    // -------------------------------------------------------------------------
    // AgentFactoryExtensions.g.cs
    // -------------------------------------------------------------------------

    [Fact]
    public void AgentFactory_SingleAgent_EmitsCreateMethod()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                public class TriageAgent { }
            }
            """;

        var output = new MafGeneratorTestRunner()
            .WithSource(source)
            .GetFile("AgentFactoryExtensions.g.cs");

        Assert.Contains("CreateTriageAgent", output);
        Assert.Contains("IAgentFactory", output);
    }

    [Fact]
    public void AgentFactory_MultipleAgents_EmitsCreateMethodPerAgent()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                public class TriageAgent { }

                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                public class ExpertAgent { }
            }
            """;

        var output = new MafGeneratorTestRunner()
            .WithSource(source)
            .GetFile("AgentFactoryExtensions.g.cs");

        Assert.Contains("CreateTriageAgent", output);
        Assert.Contains("CreateExpertAgent", output);
    }

    [Fact]
    public void AgentFactory_NoAgents_EmitsEmptyClass()
    {
        var output = MafGeneratorTestRunner.Create()
            .GetFile("AgentFactoryExtensions.g.cs");

        Assert.Contains("GeneratedAgentFactoryExtensions", output);
        Assert.DoesNotContain("CreateAgent", output);
    }

    [Fact]
    public void AgentFactory_EmittedFile_HasGeneratedCodeAttribute()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                public class TriageAgent { }
            }
            """;

        var output = new MafGeneratorTestRunner()
            .WithSource(source)
            .GetFile("AgentFactoryExtensions.g.cs");

        Assert.Contains("[global::System.CodeDom.Compiler.GeneratedCodeAttribute", output);
    }

    // -------------------------------------------------------------------------
    // AgentTopologyConstants.g.cs
    // -------------------------------------------------------------------------

    [Fact]
    public void Constants_AgentNames_EmittedPerAgent()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                public class TriageAgent { }

                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                public class ExpertAgent { }
            }
            """;

        var output = new MafGeneratorTestRunner()
            .WithSource(source)
            .GetFile("AgentTopologyConstants.g.cs");

        Assert.Contains("class AgentNames", output);
        Assert.Contains("TriageAgent", output);
        Assert.Contains("ExpertAgent", output);
    }

    [Fact]
    public void Constants_GroupNames_EmittedPerGroup()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                [NexusLabs.Needlr.AgentFramework.AgentFunctionGroup("math-tools")]
                public class MathFunctions { }

                [NexusLabs.Needlr.AgentFramework.AgentFunctionGroup("geo-tools")]
                public class GeoFunctions { }
            }
            """;

        var output = new MafGeneratorTestRunner()
            .WithSource(source)
            .GetFile("AgentTopologyConstants.g.cs");

        Assert.Contains("class GroupNames", output);
        Assert.Contains("MathTools", output);
        Assert.Contains("GeoTools", output);
        Assert.Contains("\"math-tools\"", output);
        Assert.Contains("\"geo-tools\"", output);
    }

    [Fact]
    public void Constants_PipelineNames_EmittedPerPipeline()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                [NexusLabs.Needlr.AgentFramework.AgentSequenceMember("content-pipeline", 1)]
                public class WriterAgent { }

                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                [NexusLabs.Needlr.AgentFramework.AgentSequenceMember("content-pipeline", 2)]
                public class EditorAgent { }
            }
            """;

        var output = new MafGeneratorTestRunner()
            .WithSource(source)
            .GetFile("AgentTopologyConstants.g.cs");

        Assert.Contains("class PipelineNames", output);
        Assert.Contains("ContentPipeline", output);
        Assert.Contains("\"content-pipeline\"", output);
    }

    [Fact]
    public void Constants_NoAgents_EmitsEmptyAgentNamesClass()
    {
        var output = MafGeneratorTestRunner.Create()
            .GetFile("AgentTopologyConstants.g.cs");

        Assert.Contains("class AgentNames", output);
        Assert.Contains("class GroupNames", output);
        Assert.Contains("class PipelineNames", output);
    }

    // -------------------------------------------------------------------------
    // AgentFrameworkSyringeExtensions.g.cs
    // -------------------------------------------------------------------------

    [Fact]
    public void Syringe_SingleGroup_EmitsWithMethod()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                [NexusLabs.Needlr.AgentFramework.AgentFunctionGroup("math-tools")]
                public class MathFunctions { }
            }
            """;

        var output = new MafGeneratorTestRunner()
            .WithSource(source)
            .GetFile("AgentFrameworkSyringeExtensions.g.cs");

        Assert.Contains("WithMathTools", output);
        Assert.Contains("AgentFrameworkSyringe", output);
    }

    [Fact]
    public void Syringe_MultipleGroups_EmitsWithMethodPerGroup()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                [NexusLabs.Needlr.AgentFramework.AgentFunctionGroup("math-tools")]
                public class MathFunctions { }

                [NexusLabs.Needlr.AgentFramework.AgentFunctionGroup("geo-tools")]
                public class GeoFunctions { }
            }
            """;

        var output = new MafGeneratorTestRunner()
            .WithSource(source)
            .GetFile("AgentFrameworkSyringeExtensions.g.cs");

        Assert.Contains("WithMathTools", output);
        Assert.Contains("WithGeoTools", output);
    }

    [Fact]
    public void Syringe_NoGroups_EmitsEmptyClass()
    {
        var output = MafGeneratorTestRunner.Create()
            .GetFile("AgentFrameworkSyringeExtensions.g.cs");

        Assert.Contains("GeneratedAgentFrameworkSyringeExtensions", output);
        Assert.DoesNotContain("With", output.Replace("Without", ""));
    }

    [Fact]
    public void Syringe_WithMethod_HasRequiresUnreferencedCodeAttribute()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                [NexusLabs.Needlr.AgentFramework.AgentFunctionGroup("my-tools")]
                public class MyFunctions { }
            }
            """;

        var output = new MafGeneratorTestRunner()
            .WithSource(source)
            .GetFile("AgentFrameworkSyringeExtensions.g.cs");

        Assert.Contains("RequiresUnreferencedCode", output);
        Assert.Contains("RequiresDynamicCode", output);
    }

    [Fact]
    public void Generator_EmitsAllExpectedFilesIncludingNew()
    {
        var files = MafGeneratorTestRunner.Create().RunGenerator();

        var fileNames = files.Select(f => System.IO.Path.GetFileName(f.FilePath)).ToArray();
        Assert.Contains("AgentFactoryExtensions.g.cs", fileNames);
        Assert.Contains("AgentTopologyConstants.g.cs", fileNames);
        Assert.Contains("AgentFrameworkSyringeExtensions.g.cs", fileNames);
    }
}
