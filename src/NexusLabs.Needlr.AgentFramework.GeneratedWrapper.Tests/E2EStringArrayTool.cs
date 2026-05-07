using System.ComponentModel;

namespace NexusLabs.Needlr.AgentFramework.GeneratedWrapper.Tests;

[AgentFunctionGroup("e2e-string-array")]
public sealed class E2EStringArrayTool
{
    public sealed class Capture
    {
        public string[]? Value { get; set; }
    }

    private readonly Capture _capture;

    public E2EStringArrayTool(Capture capture)
    {
        _capture = capture;
    }

    [AgentFunction]
    [Description("Records tags.")]
    public string Tag(
        [Description("Tag list.")] string[] tags)
    {
        _capture.Value = tags;
        return "ok";
    }
}
