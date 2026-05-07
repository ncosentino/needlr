using System.ComponentModel;

namespace NexusLabs.Needlr.AgentFramework.GeneratedWrapper.Tests;

[AgentFunctionGroup("e2e-temporal-array")]
public sealed class E2ETemporalArrayTool
{
    public sealed class Capture
    {
        public E2ETemporalEntry[]? Value { get; set; }
    }

    private readonly Capture _capture;

    public E2ETemporalArrayTool(Capture capture)
    {
        _capture = capture;
    }

    [AgentFunction]
    [Description("Records temporal entries.")]
    public string Record(
        [Description("Entries to record.")] E2ETemporalEntry[] entries)
    {
        _capture.Value = entries;
        return "ok";
    }
}
