using System.ComponentModel;

namespace NexusLabs.Needlr.AgentFramework.GeneratedWrapper.Tests;

[AgentFunctionGroup("e2e-optional-int")]
public sealed class E2EOptionalIntTool
{
    public static int? CapturedDefaultZero { get; set; }
    public static int? CapturedDefaultFive { get; set; }

    [AgentFunction]
    [Description("Sets a max count with a default of zero.")]
    public string SetMaxDefaultZero(
        [Description("Maximum count.")] int max = 0)
    {
        CapturedDefaultZero = max;
        return "ok";
    }

    [AgentFunction]
    [Description("Sets a max count with a default of five.")]
    public string SetMaxDefaultFive(
        [Description("Maximum count.")] int max = 5)
    {
        CapturedDefaultFive = max;
        return "ok";
    }
}
