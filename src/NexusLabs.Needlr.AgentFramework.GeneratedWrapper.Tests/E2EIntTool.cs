using System.ComponentModel;

namespace NexusLabs.Needlr.AgentFramework.GeneratedWrapper.Tests;

[AgentFunctionGroup("e2e-int")]
public sealed class E2EIntTool
{
    public static int? Captured { get; set; }

    [AgentFunction]
    [Description("Sets a max count.")]
    public string SetMax(
        [Description("Maximum count.")] int max)
    {
        Captured = max;
        return "ok";
    }
}
