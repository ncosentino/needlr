using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

using Moq;

using NexusLabs.Needlr.AgentFramework;
using NexusLabs.Needlr.AgentFramework.Workflows.Sequential;

using RfcPipelineApp.Core;

namespace RfcPipelineApp.Tests;

public sealed class RfcPipelineBuilderTests
{
    private static IReadOnlyList<PipelineStage> BuildStages()
    {
        var assignment = new RfcAssignment(
            FeatureTitle: "Auth Refactor",
            Description: "Migrate to OAuth 2.1.",
            Constraints: ["Zero downtime"],
            ExistingContext: ["Auth design doc"]);

        var stubAgent = new Mock<IChatClient>().Object
            .AsAIAgent(name: "stub", instructions: "stub");

        var agentFactory = new Mock<IAgentFactory>();
        agentFactory
            .Setup(f => f.CreateAgent(It.IsAny<Action<AgentFactoryOptions>>()))
            .Returns(stubAgent);

        var metadata = new RfcMetadata();
        var state = new RfcPipelineState(metadata);
        var logger = Mock.Of<ILogger>();

        return RfcPipelineBuilder.Build(assignment, agentFactory.Object, state, logger);
    }

    [Fact]
    public void Build_Returns16Stages()
    {
        var stages = BuildStages();

        Assert.Equal(16, stages.Count);
    }

    [Fact]
    public void Build_FirstStageIsSeedWorkspace()
    {
        var stages = BuildStages();

        Assert.Equal("SeedWorkspace", stages[0].Name);
    }

    [Fact]
    public void Build_LastStageIsFinalVerification()
    {
        var stages = BuildStages();

        Assert.Equal("FinalVerification", stages[^1].Name);
    }
}
