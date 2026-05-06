using System.ComponentModel;

namespace NexusLabs.Needlr.AgentFramework.GeneratedWrapper.Tests;

[AgentFunctionGroup("e2e-datetime")]
public sealed class E2EDateTimeTool
{
    public static DateTime? Captured { get; set; }

    [AgentFunction]
    [Description("Records a UTC moment.")]
    public string Record(
        [Description("ISO 8601 timestamp.")] DateTime when)
    {
        Captured = when;
        return "ok";
    }
}
