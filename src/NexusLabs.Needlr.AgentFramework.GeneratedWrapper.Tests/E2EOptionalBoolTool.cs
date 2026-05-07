using System.ComponentModel;

namespace NexusLabs.Needlr.AgentFramework.GeneratedWrapper.Tests;

[AgentFunctionGroup("e2e-optional-bool")]
public sealed class E2EOptionalBoolTool
{
    public sealed class Capture
    {
        public bool? DefaultFalse { get; set; }
        public bool? DefaultTrue { get; set; }
    }

    private readonly Capture _capture;

    public E2EOptionalBoolTool(Capture capture)
    {
        _capture = capture;
    }

    [AgentFunction]
    [Description("Sets a boolean flag with a default of false.")]
    public string SetFlagDefaultFalse(
        [Description("Flag value.")] bool flag = false)
    {
        _capture.DefaultFalse = flag;
        return "ok";
    }

    [AgentFunction]
    [Description("Sets a boolean flag with a default of true.")]
    public string SetFlagDefaultTrue(
        [Description("Flag value.")] bool flag = true)
    {
        _capture.DefaultTrue = flag;
        return "ok";
    }
}
