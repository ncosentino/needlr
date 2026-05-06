using System.ComponentModel;

namespace NexusLabs.Needlr.AgentFramework.GeneratedWrapper.Tests;

[AgentFunctionGroup("e2e-temporal-array")]
public sealed class E2ETemporalArrayTool
{
    public static E2ETemporalEntry[]? Captured { get; set; }

    [AgentFunction]
    [Description("Records temporal entries.")]
    public string Record(
        [Description("Entries to record.")] E2ETemporalEntry[] entries)
    {
        Captured = entries;
        return "ok";
    }
}
