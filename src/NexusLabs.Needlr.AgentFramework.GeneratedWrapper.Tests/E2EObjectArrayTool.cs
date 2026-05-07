using System.ComponentModel;

namespace NexusLabs.Needlr.AgentFramework.GeneratedWrapper.Tests;

[AgentFunctionGroup("e2e-object-array")]
public sealed class E2EObjectArrayTool
{
    public sealed class Capture
    {
        public E2EObjectEntry[]? Value { get; set; }
    }

    private readonly Capture _capture;

    public E2EObjectArrayTool(Capture capture)
    {
        _capture = capture;
    }

    [AgentFunction]
    [Description("Records entries.")]
    public string Save(
        [Description("Entries to record.")] E2EObjectEntry[] entries)
    {
        _capture.Value = entries;
        return "ok";
    }
}
