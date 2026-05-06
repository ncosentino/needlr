using System.ComponentModel;

namespace NexusLabs.Needlr.AgentFramework.GeneratedWrapper.Tests;

[AgentFunctionGroup("e2e-dto")]
public sealed class E2EDtoTool
{
    public static E2ETopicMetadata? Captured { get; set; }

    [AgentFunction]
    [Description("Records a topic metadata DTO.")]
    public string Record(
        [Description("Topic metadata.")] E2ETopicMetadata metadata)
    {
        Captured = metadata;
        return "ok";
    }
}
