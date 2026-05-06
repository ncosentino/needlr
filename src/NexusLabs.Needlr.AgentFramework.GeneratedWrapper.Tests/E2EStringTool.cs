using System.ComponentModel;

namespace NexusLabs.Needlr.AgentFramework.GeneratedWrapper.Tests;

[AgentFunctionGroup("e2e-string")]
public sealed class E2EStringTool
{
    public static string? Captured { get; set; }

    [AgentFunction]
    [Description("Records findings (mirrors the BrandGhost production crash shape).")]
    public string Record(
        [Description("JSON array of findings.")] string findingsJson)
    {
        Captured = findingsJson;
        return "ok";
    }
}
