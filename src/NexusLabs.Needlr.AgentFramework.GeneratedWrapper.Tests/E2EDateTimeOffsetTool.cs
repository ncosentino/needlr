using System.ComponentModel;

namespace NexusLabs.Needlr.AgentFramework.GeneratedWrapper.Tests;

[AgentFunctionGroup("e2e-datetimeoffset")]
public sealed class E2EDateTimeOffsetTool
{
    public sealed class Capture
    {
        public DateTimeOffset? Value { get; set; }
    }

    private readonly Capture _capture;

    public E2EDateTimeOffsetTool(Capture capture)
    {
        _capture = capture;
    }

    [AgentFunction]
    [Description("Records a moment with offset.")]
    public string Record(
        [Description("ISO 8601 timestamp with offset.")] DateTimeOffset stamp)
    {
        _capture.Value = stamp;
        return "ok";
    }
}
