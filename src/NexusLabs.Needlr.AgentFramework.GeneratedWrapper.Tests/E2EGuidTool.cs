using System.ComponentModel;

namespace NexusLabs.Needlr.AgentFramework.GeneratedWrapper.Tests;

[AgentFunctionGroup("e2e-guid")]
public sealed class E2EGuidTool
{
    public sealed class Capture
    {
        public Guid? Value { get; set; }
    }

    private readonly Capture _capture;

    public E2EGuidTool(Capture capture)
    {
        _capture = capture;
    }

    [AgentFunction]
    [Description("Records a GUID id.")]
    public string Record(
        [Description("The id.")] Guid id)
    {
        _capture.Value = id;
        return "ok";
    }
}
