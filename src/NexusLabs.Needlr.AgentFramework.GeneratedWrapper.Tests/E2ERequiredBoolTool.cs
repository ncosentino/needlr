using System.ComponentModel;

namespace NexusLabs.Needlr.AgentFramework.GeneratedWrapper.Tests;

[AgentFunctionGroup("e2e-required-bool")]
public sealed class E2ERequiredBoolTool
{
    public sealed class Capture
    {
        public bool? Value { get; set; }
    }

    private readonly Capture _capture;

    public E2ERequiredBoolTool(Capture capture)
    {
        _capture = capture;
    }

    [AgentFunction]
    [Description("Sets a required boolean flag (no default).")]
    public string SetFlag(
        [Description("Flag value.")] bool required)
    {
        _capture.Value = required;
        return "ok";
    }
}
