using System.ComponentModel;

namespace NexusLabs.Needlr.AgentFramework.GeneratedWrapper.Tests;

[AgentFunctionGroup("e2e-timespan")]
public sealed class E2ETimeSpanTool
{
    public sealed class Capture
    {
        public TimeSpan? Value { get; set; }
    }

    private readonly Capture _capture;

    public E2ETimeSpanTool(Capture capture)
    {
        _capture = capture;
    }

    [AgentFunction]
    [Description("Records a duration.")]
    public string Record(
        [Description("Duration as ISO 8601 (PT1H30M) or .NET hh:mm:ss.")] TimeSpan duration)
    {
        _capture.Value = duration;
        return "ok";
    }
}
