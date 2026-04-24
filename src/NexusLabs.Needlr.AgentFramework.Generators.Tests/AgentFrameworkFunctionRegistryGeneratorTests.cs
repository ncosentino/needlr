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

    // GeneratedAIFunctionProvider.g.cs

    [Fact]
    public void GeneratedProvider_NoFunctionTypes_EmitsEmptyProvider()
    {
        var output = MafGeneratorTestRunner.Create()
            .GetFile("GeneratedAIFunctionProvider.g.cs");

        Assert.Contains("GeneratedAIFunctionProvider", output);
        Assert.Contains("functions = null;", output);
        Assert.Contains("return false;", output);
        Assert.DoesNotContain("typeof(", output);
        Assert.DoesNotContain("if (functionType ==", output);
    }

    [Fact]
    public void GeneratedProvider_InstanceFunctionClass_EmitsTypeDispatch()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                public class Calculator
                {
                    [NexusLabs.Needlr.AgentFramework.AgentFunction]
                    public string Add(int a, int b) => (a + b).ToString();
                }
            }
            """;

        var output = new MafGeneratorTestRunner()
            .WithSource(source)
            .GetFile("GeneratedAIFunctionProvider.g.cs");

        Assert.Contains("if (functionType == typeof(global::MyApp.Calculator))", output);
        Assert.Contains("var typed = serviceProvider.GetRequiredService<global::MyApp.Calculator>();", output);
        Assert.Contains("new Calculator_Add(typed),", output);
        Assert.Contains("class Calculator_Add : global::Microsoft.Extensions.AI.AIFunction", output);
    }

    [Fact]
    public void GeneratedProvider_StaticFunctionClass_NoInstanceField()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                public static class MyStaticFunctions
                {
                    [NexusLabs.Needlr.AgentFramework.AgentFunction]
                    public static void DoWork() { }
                }
            }
            """;

        var output = new MafGeneratorTestRunner()
            .WithSource(source)
            .GetFile("GeneratedAIFunctionProvider.g.cs");

        Assert.Contains("new MyStaticFunctions_DoWork(),", output);
        Assert.DoesNotContain("_instance", output);
    }

    [Fact]
    public void GeneratedProvider_AsyncMethod_EmitsAwait()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                public class DataFetcher
                {
                    [NexusLabs.Needlr.AgentFramework.AgentFunction]
                    public async System.Threading.Tasks.Task<string> FetchData()
                        => await System.Threading.Tasks.Task.FromResult("data");
                }
            }
            """;

        var output = new MafGeneratorTestRunner()
            .WithSource(source)
            .GetFile("GeneratedAIFunctionProvider.g.cs");

        Assert.Contains("await", output);
        Assert.Contains(".ConfigureAwait(false)", output);
        Assert.Contains("async", output);
    }

    [Fact]
    public void GeneratedProvider_VoidMethod_ReturnsNullTask()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                public class Worker
                {
                    [NexusLabs.Needlr.AgentFramework.AgentFunction]
                    public void DoWork() { }
                }
            }
            """;

        var output = new MafGeneratorTestRunner()
            .WithSource(source)
            .GetFile("GeneratedAIFunctionProvider.g.cs");

        Assert.Contains("Task.FromResult<object?>(null)", output);
    }

    [Fact]
    public void GeneratedProvider_CancellationTokenParam_SkippedInSchema_PassedAsCt()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                public class Processor
                {
                    [NexusLabs.Needlr.AgentFramework.AgentFunction]
                    public string Process(string name, System.Threading.CancellationToken ct) => name;
                }
            }
            """;

        var output = new MafGeneratorTestRunner()
            .WithSource(source)
            .GetFile("GeneratedAIFunctionProvider.g.cs");

        Assert.DoesNotContain("\"ct\":", output);
        Assert.Contains("_instance.Process(name, ct)", output);
    }

    [Fact]
    public void GeneratedProvider_StringParamWithDescription_SchemaHasDescription()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                public class Greeter
                {
                    [NexusLabs.Needlr.AgentFramework.AgentFunction]
                    public string Greet([System.ComponentModel.Description("The person's name")] string name) => $"Hello {name}";
                }
            }
            """;

        var output = new MafGeneratorTestRunner()
            .WithSource(source)
            .GetFile("GeneratedAIFunctionProvider.g.cs");

        Assert.Contains("\"description\":\"The person's name\"", output);
    }

    [Fact]
    public void GeneratedProvider_RequiredIntParam_InRequiredArray()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                public class Counter
                {
                    [NexusLabs.Needlr.AgentFramework.AgentFunction]
                    public int Increment(int count) => count + 1;
                }
            }
            """;

        var output = new MafGeneratorTestRunner()
            .WithSource(source)
            .GetFile("GeneratedAIFunctionProvider.g.cs");

        Assert.Contains("\"required\":[\"count\"]", output);
    }

    [Fact]
    public void GeneratedProvider_OptionalStringParam_NotInRequiredArray()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                public class Labeler
                {
                    [NexusLabs.Needlr.AgentFramework.AgentFunction]
                    public string Label(string label = "default") => label;
                }
            }
            """;

        var output = new MafGeneratorTestRunner()
            .WithSource(source)
            .GetFile("GeneratedAIFunctionProvider.g.cs");

        Assert.DoesNotContain("\"required\":[\"label\"]", output);
    }

    [Fact]
    public void Bootstrap_RegistersAIFunctionProvider()
    {
        var output = MafGeneratorTestRunner.Create()
            .GetFile("NeedlrAgentFrameworkBootstrap.g.cs");

        Assert.Contains("RegisterAIFunctionProvider", output);
        Assert.Contains("new global::", output);
        Assert.Contains("GeneratedAIFunctionProvider()", output);
    }

    // -------------------------------------------------------------------------
    // [ProgressSinks] → generated GetXxxAgentProgressSinkTypes()
    // -------------------------------------------------------------------------

    [Fact]
    public void ProgressSinks_GeneratesCompanionMethod()
    {
        var source = """
            namespace TestApp
            {
                public class MySink : NexusLabs.Needlr.AgentFramework.Progress.IProgressSink
                {
                    public System.Threading.Tasks.ValueTask OnEventAsync(
                        NexusLabs.Needlr.AgentFramework.Progress.IProgressEvent evt,
                        System.Threading.CancellationToken ct) => default;
                }

                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent(Instructions = "test")]
                [NexusLabs.Needlr.AgentFramework.ProgressSinks(typeof(MySink))]
                public partial class TestAgent { }
            }
            """;

        var output = MafGeneratorTestRunner.Create()
            .WithSource(ProgressSinkStubs)
            .WithSource(source)
            .GetFile("AgentFactoryExtensions");

        Assert.Contains("GetTestAgentProgressSinkTypes", output);
        Assert.Contains("typeof(global::TestApp.MySink)", output);
    }

    [Fact]
    public void ProgressSinks_BeginProgressScope_DisposesSinksViaCompositeDisposable()
    {
        var source = """
            namespace TestApp
            {
                public class MySink : NexusLabs.Needlr.AgentFramework.Progress.IProgressSink, System.IDisposable
                {
                    public System.Threading.Tasks.ValueTask OnEventAsync(
                        NexusLabs.Needlr.AgentFramework.Progress.IProgressEvent evt,
                        System.Threading.CancellationToken ct) => default;
                    public void Dispose() { }
                }

                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent(Instructions = "test")]
                [NexusLabs.Needlr.AgentFramework.ProgressSinks(typeof(MySink))]
                public partial class DisposableSinkAgent { }
            }
            """;

        var output = MafGeneratorTestRunner.Create()
            .WithSource(ProgressSinkStubs)
            .WithSource(source)
            .GetFile("AgentFactoryExtensions");

        // Generated scope method must return a CompositeDisposable that
        // also disposes the instantiated sinks, not just the accessor scope.
        Assert.Contains("BeginDisposableSinkAgentProgressScope", output);
        Assert.Contains("global::NexusLabs.Needlr.AgentFramework.Progress.CompositeDisposable", output);
        Assert.Contains("global::TestApp.MySink", output);
        // The sink must be passed to the composite cast via an object box + 'as IDisposable'
        // so the cast compiles whether or not the concrete sink type implements IDisposable.
        Assert.Contains(") as global::System.IDisposable", output);
    }

    [Fact]
    public void ProgressSinks_MultipleSinks_GenerateStackingRegistrations()
    {
        var source = """
            namespace TestApp
            {
                public class SinkAlpha : NexusLabs.Needlr.AgentFramework.Progress.IProgressSink
                {
                    public System.Threading.Tasks.ValueTask OnEventAsync(
                        NexusLabs.Needlr.AgentFramework.Progress.IProgressEvent evt,
                        System.Threading.CancellationToken ct) => default;
                }

                public class SinkBeta : NexusLabs.Needlr.AgentFramework.Progress.IProgressSink
                {
                    public System.Threading.Tasks.ValueTask OnEventAsync(
                        NexusLabs.Needlr.AgentFramework.Progress.IProgressEvent evt,
                        System.Threading.CancellationToken ct) => default;
                }

                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent(Instructions = "alpha")]
                [NexusLabs.Needlr.AgentFramework.ProgressSinks(typeof(SinkAlpha))]
                public partial class AlphaAgent { }

                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent(Instructions = "beta")]
                [NexusLabs.Needlr.AgentFramework.ProgressSinks(typeof(SinkBeta))]
                public partial class BetaAgent { }
            }
            """;

        var output = MafGeneratorTestRunner.Create()
            .WithSource(ProgressSinkStubs)
            .WithSource(source)
            .GetFile("GeneratedProgressSinkRegistrations");

        // Each concrete sink must be registered once (TryAddSingleton on concrete type)
        Assert.Contains("services.TryAddSingleton<global::TestApp.SinkAlpha>()", output);
        Assert.Contains("services.TryAddSingleton<global::TestApp.SinkBeta>()", output);

        // Each sink must also be exposed as IProgressSink via AddSingleton with a
        // factory delegate so multiple sinks stack correctly in GetServices<IProgressSink>.
        // AddSingleton (not TryAddSingleton) is required because TryAddSingleton on the
        // interface would silently drop the second sink.
        Assert.Contains("services.AddSingleton<global::NexusLabs.Needlr.AgentFramework.Progress.IProgressSink>(sp => sp.GetRequiredService<global::TestApp.SinkAlpha>())", output);
        Assert.Contains("services.AddSingleton<global::NexusLabs.Needlr.AgentFramework.Progress.IProgressSink>(sp => sp.GetRequiredService<global::TestApp.SinkBeta>())", output);

        // And the old first-wins TryAddSingleton<IProgressSink, T> pattern must be gone.
        Assert.DoesNotContain("TryAddSingleton<global::NexusLabs.Needlr.AgentFramework.Progress.IProgressSink,", output);
    }

    [Fact]
    public void ProgressSinks_NotPresent_NoCompanionMethod()
    {
        var source = """
            namespace TestApp
            {
                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent(Instructions = "test")]
                public partial class PlainAgent { }
            }
            """;

        var output = MafGeneratorTestRunner.Create()
            .WithSource(source)
            .GetFile("AgentFactoryExtensions");

        Assert.DoesNotContain("ProgressSinkTypes", output);
    }

    private const string ProgressSinkStubs = """
        namespace NexusLabs.Needlr.AgentFramework.Progress
        {
            public interface IProgressEvent { }
            public interface IProgressSink
            {
                System.Threading.Tasks.ValueTask OnEventAsync(IProgressEvent evt, System.Threading.CancellationToken ct);
            }
        }
        """;

    // -------------------------------------------------------------------------
    // Array-of-objects JSON schema generation
    // -------------------------------------------------------------------------

    [Fact]
    public void AIFunctionProvider_ArrayOfObjects_EmitsFullItemSchema()
    {
        var output = MafGeneratorTestRunner.Create()
            .WithSource(ArrayOfObjectsSource)
            .GetFile("GeneratedAIFunctionProvider.g.cs");

        // The schema must include items with type:object and the properties of NoteEntry
        Assert.Contains("\"items\":{\"type\":\"object\"", output);
        Assert.Contains("\"title\":{\"type\":\"string\"", output);
        Assert.Contains("\"body\":{\"type\":\"string\"", output);
        Assert.Contains("\"required\":[\"title\",\"body\"]", output);
    }

    [Fact]
    public void AIFunctionProvider_ArrayOfObjects_UsesManualPropertyExtraction()
    {
        var output = MafGeneratorTestRunner.Create()
            .WithSource(ArrayOfObjectsSource)
            .GetFile("GeneratedAIFunctionProvider.g.cs");

        // Must use AOT-safe manual extraction (TryGetProperty), NOT JsonSerializer.Deserialize
        Assert.Contains("TryGetProperty(\"title\"", output);
        Assert.Contains("TryGetProperty(\"body\"", output);
        Assert.Contains("GetString()", output);
        Assert.DoesNotContain("JsonSerializer.Deserialize", output);
        // Must null-coalesce to empty array for non-nullable params
        Assert.Contains("Array.Empty<", output);
    }

    [Fact]
    public void AIFunctionProvider_ArrayOfObjects_IncludesPropertyDescriptions()
    {
        var output = MafGeneratorTestRunner.Create()
            .WithSource(ArrayOfObjectsSource)
            .GetFile("GeneratedAIFunctionProvider.g.cs");

        Assert.Contains("The note title", output);
        Assert.Contains("The note body", output);
    }

    [Fact]
    public void AIFunctionProvider_ArrayOfPrimitives_StillWorks()
    {
        var output = MafGeneratorTestRunner.Create()
            .WithSource(ArrayOfPrimitivesSource)
            .GetFile("GeneratedAIFunctionProvider.g.cs");

        // Primitive arrays should have items with type:string, not type:object
        Assert.Contains("\"items\":{\"type\":\"string\"}", output);
    }

    private const string ArrayOfObjectsSource = """
        using System.ComponentModel;

        namespace TestNamespace
        {
            [NexusLabs.Needlr.AgentFramework.AgentFunctionGroup("test")]
            public sealed class NoteTool
            {
                [NexusLabs.Needlr.AgentFramework.AgentFunction]
                [Description("Saves notes")]
                public string Save(
                    [Description("Array of notes")] NoteEntry[] notes)
                    => $"Saved {notes.Length}";
            }

            public sealed class NoteEntry
            {
                [Description("The note title")]
                public string Title { get; set; } = "";

                [Description("The note body")]
                public string Body { get; set; } = "";
            }
        }
        """;

    private const string ArrayOfPrimitivesSource = """
        using System.ComponentModel;

        namespace TestNamespace
        {
            [NexusLabs.Needlr.AgentFramework.AgentFunctionGroup("test")]
            public sealed class TagTool
            {
                [NexusLabs.Needlr.AgentFramework.AgentFunction]
                [Description("Sets tags")]
                public string SetTags(
                    [Description("Tag names")] string[] tags)
                    => $"Set {tags.Length} tags";
            }
        }
        """;

    // -------------------------------------------------------------------------
    // Pipeline I — AgentGraphEdge / AgentGraphEntry / AgentGraphNode
    // -------------------------------------------------------------------------

    [Fact]
    public void PipelineI_GraphEdgeAndEntry_GeneratesGraphWorkflowExtensionMethod()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                public class WebResearchAgent { }

                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                public class SummarizerAgent { }

                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                [NexusLabs.Needlr.AgentFramework.AgentGraphEntry("Research")]
                [NexusLabs.Needlr.AgentFramework.AgentGraphEdge("Research", typeof(WebResearchAgent), Condition = "NeedsWebData")]
                [NexusLabs.Needlr.AgentFramework.AgentGraphEdge("Research", typeof(SummarizerAgent))]
                public class AnalyzerAgent { }
            }
            """;

        var output = new MafGeneratorTestRunner()
            .WithSource(source)
            .GetFile("WorkflowFactoryExtensions.g.cs");

        Assert.Contains("CreateResearchGraphWorkflow", output);
        Assert.Contains("CreateGraphWorkflow", output);
    }

    [Fact]
    public void PipelineI_GraphExtensionMethod_DocListsEdges()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                public class WebAgent { }

                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                public class SummaryAgent { }

                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                [NexusLabs.Needlr.AgentFramework.AgentGraphEntry("Pipeline")]
                [NexusLabs.Needlr.AgentFramework.AgentGraphEdge("Pipeline", typeof(WebAgent), Condition = "needs-web")]
                [NexusLabs.Needlr.AgentFramework.AgentGraphEdge("Pipeline", typeof(SummaryAgent))]
                public class StartAgent { }
            }
            """;

        var output = new MafGeneratorTestRunner()
            .WithSource(source)
            .GetFile("WorkflowFactoryExtensions.g.cs");

        Assert.Contains("WebAgent", output);
        Assert.Contains("SummaryAgent", output);
        Assert.Contains("needs-web", output);
    }

    [Fact]
    public void PipelineI_GraphEdgeDiscovery_ReadsConditionAndIsRequired()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                public class OptionalAgent { }

                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                [NexusLabs.Needlr.AgentFramework.AgentGraphEntry("TestGraph")]
                [NexusLabs.Needlr.AgentFramework.AgentGraphEdge("TestGraph", typeof(OptionalAgent), Condition = "maybe", IsRequired = false)]
                public class RootAgent { }
            }
            """;

        var output = new MafGeneratorTestRunner()
            .WithSource(source)
            .GetFile("WorkflowFactoryExtensions.g.cs");

        Assert.Contains("CreateTestGraphGraphWorkflow", output);
        Assert.Contains("maybe", output);
    }

    [Fact]
    public void PipelineI_GraphNode_EmitsWithoutError()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                [NexusLabs.Needlr.AgentFramework.AgentGraphNode("Research", JoinMode = 0)]
                public class JoinerAgent { }

                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                [NexusLabs.Needlr.AgentFramework.AgentGraphEntry("Research")]
                [NexusLabs.Needlr.AgentFramework.AgentGraphEdge("Research", typeof(JoinerAgent))]
                public class StartAgent { }
            }
            """;

        var output = new MafGeneratorTestRunner()
            .WithSource(source)
            .GetFile("WorkflowFactoryExtensions.g.cs");

        Assert.Contains("CreateResearchGraphWorkflow", output);
    }

    [Fact]
    public void PipelineI_MermaidDiagram_IncludesGraphSubgraph()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                public class WebAgent { }

                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                public class SummaryAgent { }

                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                [NexusLabs.Needlr.AgentFramework.AgentGraphEntry("Research")]
                [NexusLabs.Needlr.AgentFramework.AgentGraphEdge("Research", typeof(WebAgent))]
                [NexusLabs.Needlr.AgentFramework.AgentGraphEdge("Research", typeof(SummaryAgent), Condition = "needs-summary")]
                public class AnalyzerAgent { }
            }
            """;

        var output = new MafGeneratorTestRunner()
            .WithSource(source)
            .WithDiagnostics()
            .GetFile("AgentTopologyGraph.g.cs");

        Assert.Contains("subgraph Graph_Research", output);
        Assert.Contains("AnalyzerAgent --> WebAgent", output);
        Assert.Contains("needs-summary", output);
    }

    // -------------------------------------------------------------------------
    // Pipeline I — AgentGraphTopologyRegistry emission
    // -------------------------------------------------------------------------

    [Fact]
    public void PipelineI_GraphEdgeAndEntry_EmitsGraphTopologyRegistry()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                public class WebResearchAgent { }

                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                public class SummarizerAgent { }

                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                [NexusLabs.Needlr.AgentFramework.AgentGraphEntry("Research")]
                [NexusLabs.Needlr.AgentFramework.AgentGraphEdge("Research", typeof(WebResearchAgent), Condition = "NeedsWebData")]
                [NexusLabs.Needlr.AgentFramework.AgentGraphEdge("Research", typeof(SummarizerAgent))]
                public class AnalyzerAgent { }
            }
            """;

        var output = new MafGeneratorTestRunner()
            .WithSource(source)
            .GetFile("AgentGraphTopologyRegistry.g.cs");

        Assert.Contains("AgentGraphTopologyRegistry", output);
        Assert.Contains("AllGraphs", output);
        Assert.Contains("GraphTopologyRegistration", output);
        Assert.Contains("\"Research\"", output);
        Assert.Contains("AnalyzerAgent", output);
        Assert.Contains("WebResearchAgent", output);
        Assert.Contains("SummarizerAgent", output);
        Assert.Contains("NeedsWebData", output);
    }

    [Fact]
    public void PipelineI_EmptyGraph_StillEmitsGraphTopologyRegistry()
    {
        var output = MafGeneratorTestRunner.Create()
            .GetFile("AgentGraphTopologyRegistry.g.cs");

        Assert.Contains("AgentGraphTopologyRegistry", output);
        Assert.Contains("AllGraphs", output);
    }

    [Fact]
    public void PipelineI_Bootstrap_IncludesRegisterGraphTopologyCall()
    {
        var output = MafGeneratorTestRunner.Create()
            .GetFile("NeedlrAgentFrameworkBootstrap.g.cs");

        Assert.Contains("RegisterGraphTopology", output);
        Assert.Contains("AgentGraphTopologyRegistry.AllGraphs", output);
    }

    // -------------------------------------------------------------------------
    // Pipeline I — NodeRoutingMode on edges (P0 fix)
    // -------------------------------------------------------------------------

    [Fact]
    public void PipelineI_GraphEdge_NodeRoutingMode_EmittedInRegistry()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                public class TargetAgent { }

                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                [NexusLabs.Needlr.AgentFramework.AgentGraphEntry("Routing")]
                [NexusLabs.Needlr.AgentFramework.AgentGraphEdge("Routing", typeof(TargetAgent), NodeRoutingMode = 2)]
                public class SourceAgent { }
            }
            """;

        var output = new MafGeneratorTestRunner()
            .WithSource(source)
            .GetFile("AgentGraphTopologyRegistry.g.cs");

        // The emitted edge tuple must include the NodeRoutingMode value (2 = FirstMatching)
        Assert.Contains("(int?)2", output);
        // Verify the 5-element edge tuple shape is present
        Assert.Contains("int?", output);
    }

    [Fact]
    public void PipelineI_GraphEdge_NoNodeRoutingMode_EmitsNull()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                public class TargetAgent { }

                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                [NexusLabs.Needlr.AgentFramework.AgentGraphEntry("NoRoute")]
                [NexusLabs.Needlr.AgentFramework.AgentGraphEdge("NoRoute", typeof(TargetAgent))]
                public class SourceAgent { }
            }
            """;

        var output = new MafGeneratorTestRunner()
            .WithSource(source)
            .GetFile("AgentGraphTopologyRegistry.g.cs");

        // When NodeRoutingMode is not set, the 5th element should be null
        Assert.Contains("(int?)null", output);
    }

    // -------------------------------------------------------------------------
    // Pipeline I — coverage: edge cases
    // -------------------------------------------------------------------------

    [Fact]
    public void PipelineI_GraphEntryWithNoEdges_EmitsRegistryWithEmptyEdges()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                [NexusLabs.Needlr.AgentFramework.AgentGraphEntry("Lonely")]
                public class LonelyAgent { }
            }
            """;

        var output = new MafGeneratorTestRunner()
            .WithSource(source)
            .GetFile("AgentGraphTopologyRegistry.g.cs");

        Assert.Contains("\"Lonely\"", output);
        Assert.Contains("LonelyAgent", output);
    }

    [Fact]
    public void PipelineI_GraphEntryWithNoAgentAttribute_StillEmitsGraph()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                [NexusLabs.Needlr.AgentFramework.AgentGraphEntry("Plain")]
                public class PlainAgent { }
            }
            """;

        var output = new MafGeneratorTestRunner()
            .WithSource(source)
            .GetFile("AgentGraphTopologyRegistry.g.cs");

        // Graph entry should still appear even without [NeedlrAiAgent]
        Assert.Contains("\"Plain\"", output);
    }

    [Fact]
    public void PipelineI_MultipleGraphsInSameAssembly_BothAppearInRegistry()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                public class Worker1 { }

                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                public class Worker2 { }

                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                [NexusLabs.Needlr.AgentFramework.AgentGraphEntry("Alpha")]
                [NexusLabs.Needlr.AgentFramework.AgentGraphEdge("Alpha", typeof(Worker1))]
                public class AlphaEntry { }

                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                [NexusLabs.Needlr.AgentFramework.AgentGraphEntry("Beta")]
                [NexusLabs.Needlr.AgentFramework.AgentGraphEdge("Beta", typeof(Worker2))]
                public class BetaEntry { }
            }
            """;

        var output = new MafGeneratorTestRunner()
            .WithSource(source)
            .GetFile("AgentGraphTopologyRegistry.g.cs");

        Assert.Contains("\"Alpha\"", output);
        Assert.Contains("\"Beta\"", output);
        Assert.Contains("AlphaEntry", output);
        Assert.Contains("BetaEntry", output);
        Assert.Contains("Worker1", output);
        Assert.Contains("Worker2", output);
    }

    // -------------------------------------------------------------------------
    // Issue #2 — Silent reducer drop when ReducerMethod is omitted
    // -------------------------------------------------------------------------

    [Fact]
    public void PipelineI_GraphReducer_NoReducerMethod_DefaultsToReduce()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                public class WorkerAgent { }

                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                [NexusLabs.Needlr.AgentFramework.AgentGraphEntry("G")]
                [NexusLabs.Needlr.AgentFramework.AgentGraphEdge("G", typeof(WorkerAgent))]
                [NexusLabs.Needlr.AgentFramework.AgentGraphReducer("G")]
                public class OrchestratorAgent { }
            }
            """;

        var output = new MafGeneratorTestRunner()
            .WithSource(source)
            .GetFile("AgentGraphTopologyRegistry.g.cs");

        // The reducer entry should exist with default method name "Reduce"
        Assert.Contains("\"Reduce\"", output);
        Assert.Contains("OrchestratorAgent", output);
    }

    [Fact]
    public void PipelineI_GraphReducer_ExplicitReducerMethod_UsesProvidedName()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                public class WorkerAgent { }

                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                [NexusLabs.Needlr.AgentFramework.AgentGraphEntry("G")]
                [NexusLabs.Needlr.AgentFramework.AgentGraphEdge("G", typeof(WorkerAgent))]
                [NexusLabs.Needlr.AgentFramework.AgentGraphReducer("G", ReducerMethod = "MergeResults")]
                public class OrchestratorAgent { }
            }
            """;

        var output = new MafGeneratorTestRunner()
            .WithSource(source)
            .GetFile("AgentGraphTopologyRegistry.g.cs");

        Assert.Contains("\"MergeResults\"", output);
    }

    // -------------------------------------------------------------------------
    // Issue #6 — Mermaid diagram enhancements for graphs
    // -------------------------------------------------------------------------

    [Fact]
    public void PipelineI_MermaidDiagram_EntryPoint_UsesStadiumShape()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                public class WorkerAgent { }

                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                [NexusLabs.Needlr.AgentFramework.AgentGraphEntry("Flow")]
                [NexusLabs.Needlr.AgentFramework.AgentGraphEdge("Flow", typeof(WorkerAgent))]
                public class EntryAgent { }
            }
            """;

        var output = new MafGeneratorTestRunner()
            .WithSource(source)
            .WithDiagnostics()
            .GetFile("AgentTopologyGraph.g.cs");

        // Entry point should use stadium shape ([name])
        Assert.Contains("([EntryAgent])", output);
    }

    [Fact]
    public void PipelineI_MermaidDiagram_OptionalEdge_UsesDashedArrow()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                public class OptionalTarget { }

                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                [NexusLabs.Needlr.AgentFramework.AgentGraphEntry("Flow")]
                [NexusLabs.Needlr.AgentFramework.AgentGraphEdge("Flow", typeof(OptionalTarget), IsRequired = false)]
                public class SourceAgent { }
            }
            """;

        var output = new MafGeneratorTestRunner()
            .WithSource(source)
            .WithDiagnostics()
            .GetFile("AgentTopologyGraph.g.cs");

        // Optional edges should use dashed arrow
        Assert.Contains("-.->", output);
    }

    [Fact]
    public void PipelineI_MermaidDiagram_ReducerNode_UsesHexagonShape()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                public class WorkerAgent { }

                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                [NexusLabs.Needlr.AgentFramework.AgentGraphReducer("Flow", ReducerMethod = "Merge")]
                public class ReducerAgent { }

                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                [NexusLabs.Needlr.AgentFramework.AgentGraphEntry("Flow")]
                [NexusLabs.Needlr.AgentFramework.AgentGraphEdge("Flow", typeof(WorkerAgent))]
                public class EntryAgent { }
            }
            """;

        var output = new MafGeneratorTestRunner()
            .WithSource(source)
            .WithDiagnostics()
            .GetFile("AgentTopologyGraph.g.cs");

        // Reducer should use hexagon shape
        Assert.Contains("{{ReducerAgent}}", output);
    }

    [Fact]
    public void PipelineI_MermaidDiagram_JoinModeNonDefault_ShowsLabel()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                [NexusLabs.Needlr.AgentFramework.AgentGraphNode("Flow", JoinMode = 1)]
                public class JoinAgent { }

                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                [NexusLabs.Needlr.AgentFramework.AgentGraphEntry("Flow")]
                [NexusLabs.Needlr.AgentFramework.AgentGraphEdge("Flow", typeof(JoinAgent))]
                public class SourceAgent { }
            }
            """;

        var files = new MafGeneratorTestRunner()
            .WithSource(source)
            .WithDiagnostics()
            .RunGenerator();

        var graphFile = files.FirstOrDefault(f => f.FilePath.Contains("AgentTopologyGraph"));
        Assert.NotNull(graphFile);
        var output = graphFile.Content;

        // JoinMode=1 (WaitAny) should show a label (quotes are doubled for verbatim string embedding)
        Assert.Contains("WaitAny", output);
    }

    [Fact]
    public void PipelineI_MermaidDiagram_ConditionOnEdge_ShowsLabel()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                public class WebAgent { }

                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                [NexusLabs.Needlr.AgentFramework.AgentGraphEntry("Flow")]
                [NexusLabs.Needlr.AgentFramework.AgentGraphEdge("Flow", typeof(WebAgent), Condition = "needs-web")]
                public class RouterAgent { }
            }
            """;

        var output = new MafGeneratorTestRunner()
            .WithSource(source)
            .WithDiagnostics()
            .GetFile("AgentTopologyGraph.g.cs");

        // Condition text should appear on the edge
        Assert.Contains("needs-web", output);
    }

    // -------------------------------------------------------------------------
    // Issue #7 — GraphNames constants class
    // -------------------------------------------------------------------------

    [Fact]
    public void Constants_GraphNames_EmittedPerGraph()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                public class WorkerAgent { }

                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                [NexusLabs.Needlr.AgentFramework.AgentGraphEntry("Research")]
                [NexusLabs.Needlr.AgentFramework.AgentGraphEdge("Research", typeof(WorkerAgent))]
                public class ResearchEntry { }
            }
            """;

        var output = new MafGeneratorTestRunner()
            .WithSource(source)
            .GetFile("AgentTopologyConstants.g.cs");

        Assert.Contains("class GraphNames", output);
        Assert.Contains("\"Research\"", output);
    }

    [Fact]
    public void Constants_NoGraphs_EmitsEmptyGraphNamesClass()
    {
        var output = MafGeneratorTestRunner.Create()
            .GetFile("AgentTopologyConstants.g.cs");

        Assert.Contains("class GraphNames", output);
    }

    // -------------------------------------------------------------------------
    // Pipeline I — Run{Name}GraphWorkflowAsync helper emission
    // -------------------------------------------------------------------------

    [Fact]
    public void PipelineI_GraphWorkflow_EmitsRunGraphWorkflowAsyncHelper()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                public class WorkerAgent { }

                [NexusLabs.Needlr.AgentFramework.NeedlrAiAgent]
                [NexusLabs.Needlr.AgentFramework.AgentGraphEntry("Research")]
                [NexusLabs.Needlr.AgentFramework.AgentGraphEdge("Research", typeof(WorkerAgent))]
                public class AnalyzerAgent { }
            }
            """;

        var output = new MafGeneratorTestRunner()
            .WithSource(source)
            .GetFile("WorkflowFactoryExtensions.g.cs");

        Assert.Contains("RunResearchGraphWorkflowAsync", output);
        Assert.Contains("IGraphWorkflowRunner", output);
        Assert.Contains("IDagRunResult", output);
        Assert.Contains("RunGraphAsync", output);
        Assert.Contains("IProgressReporter", output);
    }

    // -------------------------------------------------------------------------
    // ReturnJsonSchema emission
    // -------------------------------------------------------------------------

    [Fact]
    public void GeneratedProvider_StringReturn_EmitsReturnJsonSchema()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                public class Tools
                {
                    [NexusLabs.Needlr.AgentFramework.AgentFunction]
                    public string Search(string query) => "result";
                }
            }
            """;

        var output = new MafGeneratorTestRunner()
            .WithSource(source)
            .GetFile("GeneratedAIFunctionProvider.g.cs");

        Assert.Contains("_returnSchema", output);
        Assert.Contains("ReturnJsonSchema => _returnSchema", output);
        Assert.Contains("\"type\":\"string\"", output);
    }

    [Fact]
    public void GeneratedProvider_IntReturn_EmitsIntegerReturnSchema()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                public class Tools
                {
                    [NexusLabs.Needlr.AgentFramework.AgentFunction]
                    public int Count(string input) => input.Length;
                }
            }
            """;

        var output = new MafGeneratorTestRunner()
            .WithSource(source)
            .GetFile("GeneratedAIFunctionProvider.g.cs");

        Assert.Contains("ReturnJsonSchema => _returnSchema", output);
        Assert.Contains("\"type\":\"integer\"", output);
    }

    [Fact]
    public void GeneratedProvider_VoidReturn_DoesNotEmitReturnSchema()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                public static class Tools
                {
                    [NexusLabs.Needlr.AgentFramework.AgentFunction]
                    public static void DoWork() { }
                }
            }
            """;

        var output = new MafGeneratorTestRunner()
            .WithSource(source)
            .GetFile("GeneratedAIFunctionProvider.g.cs");

        Assert.DoesNotContain("ReturnJsonSchema", output);
        Assert.DoesNotContain("_returnSchema", output);
    }

    [Fact]
    public void GeneratedProvider_TaskReturn_DoesNotEmitReturnSchema()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                public class Tools
                {
                    [NexusLabs.Needlr.AgentFramework.AgentFunction]
                    public async System.Threading.Tasks.Task DoWorkAsync()
                        => await System.Threading.Tasks.Task.CompletedTask;
                }
            }
            """;

        var output = new MafGeneratorTestRunner()
            .WithSource(source)
            .GetFile("GeneratedAIFunctionProvider.g.cs");

        Assert.DoesNotContain("ReturnJsonSchema", output);
        Assert.DoesNotContain("_returnSchema", output);
    }

    [Fact]
    public void GeneratedProvider_TaskOfStringReturn_EmitsReturnSchema()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                public class Tools
                {
                    [NexusLabs.Needlr.AgentFramework.AgentFunction]
                    public async System.Threading.Tasks.Task<string> FetchAsync()
                        => await System.Threading.Tasks.Task.FromResult("data");
                }
            }
            """;

        var output = new MafGeneratorTestRunner()
            .WithSource(source)
            .GetFile("GeneratedAIFunctionProvider.g.cs");

        Assert.Contains("ReturnJsonSchema => _returnSchema", output);
        Assert.Contains("\"type\":\"string\"", output);
    }

    [Fact]
    public void GeneratedProvider_ComplexObjectReturn_EmitsPropertyLevelSchema()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                public class SearchResult
                {
                    public string Title { get; set; } = "";
                    public int Score { get; set; }
                    public bool IsRelevant { get; set; }
                }

                public class Tools
                {
                    [NexusLabs.Needlr.AgentFramework.AgentFunction]
                    public SearchResult Search(string query) => new();
                }
            }
            """;

        var output = new MafGeneratorTestRunner()
            .WithSource(source)
            .GetFile("GeneratedAIFunctionProvider.g.cs");

        Assert.Contains("ReturnJsonSchema => _returnSchema", output);
        Assert.Contains("\"type\":\"object\"", output);
        Assert.Contains("\"properties\"", output);
        Assert.Contains("\"title\"", output);
        Assert.Contains("\"score\"", output);
        Assert.Contains("\"isRelevant\"", output);
    }

    [Fact]
    public void GeneratedProvider_BoolReturn_EmitsReturnSchema()
    {
        var source = MafGeneratorTestRunner.MafAttributeDefinitions + """
            namespace MyApp
            {
                public class Tools
                {
                    [NexusLabs.Needlr.AgentFramework.AgentFunction]
                    public bool Validate(string input) => true;
                }
            }
            """;

        var output = new MafGeneratorTestRunner()
            .WithSource(source)
            .GetFile("GeneratedAIFunctionProvider.g.cs");

        Assert.Contains("ReturnJsonSchema => _returnSchema", output);
        Assert.Contains("\"type\":\"boolean\"", output);
    }
}
