using System.ComponentModel;

namespace NexusLabs.Needlr.AgentFramework.GeneratedWrapper.Tests;

[AgentFunctionGroup("e2e-bool")]
public sealed class E2EBoolTool
{
    public static bool? Captured { get; set; }

    [AgentFunction]
    [Description("Sets a boolean flag.")]
    public string SetFlag(
        [Description("Flag value.")] bool flag)
    {
        Captured = flag;
        return "ok";
    }
}
