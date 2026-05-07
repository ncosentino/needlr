using System.ComponentModel;

namespace NexusLabs.Needlr.AgentFramework.GeneratedWrapper.Tests;

[AgentFunctionGroup("e2e-dto")]
public sealed class E2EDtoTool
{
    public sealed class Capture
    {
        public E2ETopicMetadata? Value { get; set; }
    }

    private readonly Capture _capture;

    public E2EDtoTool(Capture capture)
    {
        _capture = capture;
    }

    [AgentFunction]
    [Description("Records a topic metadata DTO.")]
    public string Record(
        [Description("Topic metadata.")] E2ETopicMetadata metadata)
    {
        _capture.Value = metadata;
        return "ok";
    }
}
