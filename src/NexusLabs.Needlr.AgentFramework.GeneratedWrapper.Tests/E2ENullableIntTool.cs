using System.ComponentModel;

namespace NexusLabs.Needlr.AgentFramework.GeneratedWrapper.Tests;

[AgentFunctionGroup("e2e-nullable-int")]
public sealed class E2ENullableIntTool
{
    public static int? CapturedDefaultNull { get; set; }
    public static bool CapturedDefaultNullWasSet { get; set; }
    public static int? CapturedDefaultFive { get; set; }

    [AgentFunction]
    [Description("Records an offset with a nullable-int default of null.")]
    public string RecordDefaultNull(
        [Description("Offset value.")] int? offset = null)
    {
        CapturedDefaultNull = offset;
        CapturedDefaultNullWasSet = true;
        return "ok";
    }

    [AgentFunction]
    [Description("Records an offset with a nullable-int default of 5.")]
    public string RecordDefaultFive(
        [Description("Offset value.")] int? offset = 5)
    {
        CapturedDefaultFive = offset;
        return "ok";
    }
}
