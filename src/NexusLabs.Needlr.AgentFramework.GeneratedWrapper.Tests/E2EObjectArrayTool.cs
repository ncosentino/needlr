using System.ComponentModel;

namespace NexusLabs.Needlr.AgentFramework.GeneratedWrapper.Tests;

[AgentFunctionGroup("e2e-object-array")]
public sealed class E2EObjectArrayTool
{
    public static E2EObjectEntry[]? Captured { get; set; }

    [AgentFunction]
    [Description("Records entries.")]
    public string Save(
        [Description("Entries to record.")] E2EObjectEntry[] entries)
    {
        Captured = entries;
        return "ok";
    }
}
