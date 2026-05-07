using System.ComponentModel;

namespace NexusLabs.Needlr.AgentFramework.GeneratedWrapper.Tests;

public enum E2EMode
{
    Read,
    Write,
    Append,
}

[AgentFunctionGroup("e2e-enum")]
public sealed class E2EEnumTool
{
    public sealed class Capture
    {
        public E2EMode? Required { get; set; }
        public E2EMode? Default { get; set; }
    }

    private readonly Capture _capture;

    public E2EEnumTool(Capture capture)
    {
        _capture = capture;
    }

    [AgentFunction]
    [Description("Sets the mode (required, no default).")]
    public string SetMode(
        [Description("Mode value.")] E2EMode mode)
    {
        _capture.Required = mode;
        return "ok";
    }

    [AgentFunction]
    [Description("Sets the mode with a default of Append.")]
    public string SetModeDefault(
        [Description("Mode value.")] E2EMode mode = E2EMode.Append)
    {
        _capture.Default = mode;
        return "ok";
    }
}
