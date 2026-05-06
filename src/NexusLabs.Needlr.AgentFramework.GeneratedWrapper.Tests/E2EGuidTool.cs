using System.ComponentModel;

namespace NexusLabs.Needlr.AgentFramework.GeneratedWrapper.Tests;

[AgentFunctionGroup("e2e-guid")]
public sealed class E2EGuidTool
{
    public static Guid? Captured { get; set; }

    [AgentFunction]
    [Description("Records a GUID id.")]
    public string Record(
        [Description("The id.")] Guid id)
    {
        Captured = id;
        return "ok";
    }
}
