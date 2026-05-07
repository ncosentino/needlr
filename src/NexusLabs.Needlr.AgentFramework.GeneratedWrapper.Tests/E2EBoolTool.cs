using System.ComponentModel;

namespace NexusLabs.Needlr.AgentFramework.GeneratedWrapper.Tests;

[AgentFunctionGroup("e2e-bool")]
public sealed class E2EBoolTool
{
    public sealed class Capture
    {
        public bool? Value { get; set; }
    }

    private readonly Capture _capture;

    public E2EBoolTool(Capture capture)
    {
        _capture = capture;
    }

    [AgentFunction]
    [Description("Sets a boolean flag.")]
    public string SetFlag(
        [Description("Flag value.")] bool flag)
    {
        _capture.Value = flag;
        return "ok";
    }
}
