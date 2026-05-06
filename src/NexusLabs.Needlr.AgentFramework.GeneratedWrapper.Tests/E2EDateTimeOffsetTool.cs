using System.ComponentModel;

namespace NexusLabs.Needlr.AgentFramework.GeneratedWrapper.Tests;

[AgentFunctionGroup("e2e-datetimeoffset")]
public sealed class E2EDateTimeOffsetTool
{
    public static DateTimeOffset? Captured { get; set; }

    [AgentFunction]
    [Description("Records a moment with offset.")]
    public string Record(
        [Description("ISO 8601 timestamp with offset.")] DateTimeOffset stamp)
    {
        Captured = stamp;
        return "ok";
    }
}
