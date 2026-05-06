using System.ComponentModel;

namespace NexusLabs.Needlr.AgentFramework.GeneratedWrapper.Tests;

[AgentFunctionGroup("e2e-required-bool")]
public sealed class E2ERequiredBoolTool
{
    public static bool? Captured { get; set; }

    [AgentFunction]
    [Description("Sets a required boolean flag (no default).")]
    public string SetFlag(
        [Description("Flag value.")] bool required)
    {
        Captured = required;
        return "ok";
    }
}
