using System.ComponentModel;

namespace NexusLabs.Needlr.AgentFramework.GeneratedWrapper.Tests;

[AgentFunctionGroup("e2e-optional-bool")]
public sealed class E2EOptionalBoolTool
{
    public static bool? CapturedDefaultFalse { get; set; }
    public static bool? CapturedDefaultTrue { get; set; }

    [AgentFunction]
    [Description("Sets a boolean flag with a default of false.")]
    public string SetFlagDefaultFalse(
        [Description("Flag value.")] bool flag = false)
    {
        CapturedDefaultFalse = flag;
        return "ok";
    }

    [AgentFunction]
    [Description("Sets a boolean flag with a default of true.")]
    public string SetFlagDefaultTrue(
        [Description("Flag value.")] bool flag = true)
    {
        CapturedDefaultTrue = flag;
        return "ok";
    }
}
