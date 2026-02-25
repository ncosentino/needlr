using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;

namespace NexusLabs.Needlr.AgentFramework.Tests;

/// <summary>
/// Tests for <see cref="WorkflowFactory.CreateSequentialWorkflow(string)"/> covering both the
/// reflection discovery path and the source-generated bootstrap path (via
/// <see cref="AgentFrameworkGeneratedBootstrap.BeginTestScope"/>), plus parity between them.
/// </summary>
public class WorkflowFactorySequentialTests
{
    private static IWorkflowFactory BuildWorkflowFactory(Func<AgentFrameworkSyringe, AgentFrameworkSyringe> configure)
    {
        var config = new ConfigurationBuilder().Build();
        var mockChatClient = new Mock<IChatClient>();
        return new Syringe()
            .UsingReflection()
            .UsingAgentFramework(af => configure(
                af.Configure(opts => opts.ChatClientFactory = _ => mockChatClient.Object)))
            .BuildServiceProvider(config)
            .GetRequiredService<IWorkflowFactory>();
    }

    private static IWorkflowFactory BuildDefaultWorkflowFactory() =>
        BuildWorkflowFactory(af => af
            .AddAgent<WriterSeqWfAgent>()
            .AddAgent<EditorSeqWfAgent>()
            .AddAgent<PublisherSeqWfAgent>());

    // -------------------------------------------------------------------------
    // Sequential — reflection path
    // -------------------------------------------------------------------------

    [Fact]
    public void CreateSequentialWorkflow_ReflectionPath_ThreeAgents_ReturnsNonNullWorkflow()
    {
        var factory = BuildDefaultWorkflowFactory();

        var workflow = factory.CreateSequentialWorkflow("wf-content-pipeline");

        Assert.NotNull(workflow);
        Assert.IsAssignableFrom<Workflow>(workflow);
    }

    [Fact]
    public void CreateSequentialWorkflow_ReflectionPath_UnknownPipeline_ThrowsInvalidOperation()
    {
        var factory = BuildDefaultWorkflowFactory();

        Assert.Throws<InvalidOperationException>(() =>
            factory.CreateSequentialWorkflow("no-such-pipeline"));
    }

    [Fact]
    public void CreateSequentialWorkflow_ReflectionPath_ThreeAgents_HasExecutors()
    {
        var factory = BuildDefaultWorkflowFactory();

        var workflow = factory.CreateSequentialWorkflow("wf-content-pipeline");

        Assert.True(workflow.ReflectExecutors().Count > 0);
    }

    // -------------------------------------------------------------------------
    // Sequential — bootstrap path
    // -------------------------------------------------------------------------

    [Fact]
    public void CreateSequentialWorkflow_BootstrapPath_ThreeAgents_ReturnsNonNullWorkflow()
    {
        var factory = BuildDefaultWorkflowFactory();

        using var scope = AgentFrameworkGeneratedBootstrap.BeginTestScope(
            functionTypes: static () => [],
            groupTypes: static () => new Dictionary<string, IReadOnlyList<Type>>(),
            agentTypes: static () => [typeof(WriterSeqWfAgent), typeof(EditorSeqWfAgent), typeof(PublisherSeqWfAgent)],
            sequentialTopology: static () => new Dictionary<string, IReadOnlyList<Type>>
            {
                ["wf-content-pipeline"] = [typeof(WriterSeqWfAgent), typeof(EditorSeqWfAgent), typeof(PublisherSeqWfAgent)]
            });

        var workflow = factory.CreateSequentialWorkflow("wf-content-pipeline");

        Assert.NotNull(workflow);
        Assert.IsAssignableFrom<Workflow>(workflow);
    }

    [Fact]
    public void CreateSequentialWorkflow_BootstrapPath_UnknownPipeline_FallsBackToReflection()
    {
        var factory = BuildDefaultWorkflowFactory();

        // Bootstrap active but the pipeline isn't registered — falls back to reflection.
        // WriterSeqWfAgent/EditorSeqWfAgent/PublisherSeqWfAgent carry [AgentSequenceMember("wf-content-pipeline")]
        // so reflection succeeds.
        using var scope = AgentFrameworkGeneratedBootstrap.BeginTestScope(
            functionTypes: static () => [],
            groupTypes: static () => new Dictionary<string, IReadOnlyList<Type>>(),
            agentTypes: static () => [],
            sequentialTopology: static () => new Dictionary<string, IReadOnlyList<Type>>());

        var workflow = factory.CreateSequentialWorkflow("wf-content-pipeline");

        Assert.NotNull(workflow);
    }

    // -------------------------------------------------------------------------
    // Sequential — parity
    // -------------------------------------------------------------------------

    [Fact]
    public void CreateSequentialWorkflow_BootstrapAndReflectionPaths_SameAgentCount()
    {
        var factory = BuildDefaultWorkflowFactory();

        // Reflection path (no scope active)
        var reflectionWorkflow = factory.CreateSequentialWorkflow("wf-content-pipeline");

        // Bootstrap path (scope active with matching pipeline)
        using var scope = AgentFrameworkGeneratedBootstrap.BeginTestScope(
            functionTypes: static () => [],
            groupTypes: static () => new Dictionary<string, IReadOnlyList<Type>>(),
            agentTypes: static () => [typeof(WriterSeqWfAgent), typeof(EditorSeqWfAgent), typeof(PublisherSeqWfAgent)],
            sequentialTopology: static () => new Dictionary<string, IReadOnlyList<Type>>
            {
                ["wf-content-pipeline"] = [typeof(WriterSeqWfAgent), typeof(EditorSeqWfAgent), typeof(PublisherSeqWfAgent)]
            });

        var bootstrapWorkflow = factory.CreateSequentialWorkflow("wf-content-pipeline");

        Assert.NotNull(reflectionWorkflow);
        Assert.NotNull(bootstrapWorkflow);
        Assert.Equal(
            reflectionWorkflow.ReflectExecutors().Count,
            bootstrapWorkflow.ReflectExecutors().Count);
    }
}

// ---------------------------------------------------------------------------
// Test agents — sequential pipeline "wf-content-pipeline"
// ---------------------------------------------------------------------------

[NeedlrAiAgent(Instructions = "Draft raw content for a topic.")]
[AgentSequenceMember("wf-content-pipeline", 1)]
public sealed class WriterSeqWfAgent { }

[NeedlrAiAgent(Instructions = "Edit and improve a content draft.")]
[AgentSequenceMember("wf-content-pipeline", 2)]
public sealed class EditorSeqWfAgent { }

[NeedlrAiAgent(Instructions = "Format content for publication.")]
[AgentSequenceMember("wf-content-pipeline", 3)]
public sealed class PublisherSeqWfAgent { }
