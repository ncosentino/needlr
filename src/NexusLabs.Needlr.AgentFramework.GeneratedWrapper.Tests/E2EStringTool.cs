using System.ComponentModel;

namespace NexusLabs.Needlr.AgentFramework.GeneratedWrapper.Tests;

[AgentFunctionGroup("e2e-string")]
public sealed class E2EStringTool
{
    public sealed class Capture
    {
        public string? Value { get; set; }
    }

    private readonly Capture _capture;

    public E2EStringTool(Capture capture)
    {
        _capture = capture;
    }

    [AgentFunction]
    [Description("Records findings (mirrors the BrandGhost production crash shape).")]
    public string Record(
        [Description("JSON array of findings.")] string findingsJson)
    {
        _capture.Value = findingsJson;
        return "ok";
    }
}
