using Xunit;

namespace NexusLabs.Needlr.AgentFramework.Generators.Tests;

/// <summary>
/// Tests for <see cref="AgentFrameworkFunctionRegistryGenerator"/> covering all six pipelines:
/// A (AgentFunction), B (AgentFunctionGroup), C (NeedlrAiAgent),
/// D (AgentHandoffsTo), E (AgentGroupChatMember), F (AgentSequenceMember),
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
}
