using System.ComponentModel;

namespace NexusLabs.Needlr.AgentFramework.GeneratedWrapper.Tests;

[AgentFunctionGroup("e2e-string-array")]
public sealed class E2EStringArrayTool
{
    public static string[]? Captured { get; set; }

    [AgentFunction]
    [Description("Records tags.")]
    public string Tag(
        [Description("Tag list.")] string[] tags)
    {
        Captured = tags;
        return "ok";
    }
}
