using System.ComponentModel;

namespace NexusLabs.Needlr.AgentFramework.GeneratedWrapper.Tests;

[AgentFunctionGroup("e2e-int")]
public sealed class E2EIntTool
{
    public sealed class Capture
    {
        public int? Value { get; set; }
    }

    private readonly Capture _capture;

    public E2EIntTool(Capture capture)
    {
        _capture = capture;
    }

    [AgentFunction]
    [Description("Sets a max count.")]
    public string SetMax(
        [Description("Maximum count.")] int max)
    {
        _capture.Value = max;
        return "ok";
    }
}
