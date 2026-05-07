using System.ComponentModel;

namespace NexusLabs.Needlr.AgentFramework.GeneratedWrapper.Tests;

[AgentFunctionGroup("e2e-datetime")]
public sealed class E2EDateTimeTool
{
    public sealed class Capture
    {
        public DateTime? Value { get; set; }
    }

    private readonly Capture _capture;

    public E2EDateTimeTool(Capture capture)
    {
        _capture = capture;
    }

    [AgentFunction]
    [Description("Records a UTC moment.")]
    public string Record(
        [Description("ISO 8601 timestamp.")] DateTime when)
    {
        _capture.Value = when;
        return "ok";
    }
}
