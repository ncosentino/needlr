using System.ComponentModel;

namespace NexusLabs.Needlr.AgentFramework.GeneratedWrapper.Tests;

[AgentFunctionGroup("e2e-timespan")]
public sealed class E2ETimeSpanTool
{
    public static TimeSpan? Captured { get; set; }

    [AgentFunction]
    [Description("Records a duration.")]
    public string Record(
        [Description("Duration as ISO 8601 (PT1H30M) or .NET hh:mm:ss.")] TimeSpan duration)
    {
        Captured = duration;
        return "ok";
    }
}
