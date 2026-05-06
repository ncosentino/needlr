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
    public static E2EMode? CapturedRequired { get; set; }
    public static E2EMode? CapturedDefault { get; set; }

    [AgentFunction]
    [Description("Sets the mode (required, no default).")]
    public string SetMode(
        [Description("Mode value.")] E2EMode mode)
    {
        CapturedRequired = mode;
        return "ok";
    }

    [AgentFunction]
    [Description("Sets the mode with a default of Append.")]
    public string SetModeDefault(
        [Description("Mode value.")] E2EMode mode = E2EMode.Append)
    {
        CapturedDefault = mode;
        return "ok";
    }
}
