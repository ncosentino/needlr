using System.ComponentModel;

namespace NexusLabs.Needlr.AgentFramework.GeneratedWrapper.Tests;

[AgentFunctionGroup("e2e-optional-string")]
public sealed class E2EOptionalStringTool
{
    public static string? CapturedDefaultNull { get; set; }
    public static bool CapturedDefaultNullWasSet { get; set; }
    public static string? CapturedDefaultLiteral { get; set; }

    [AgentFunction]
    [Description("Records a label with a nullable default of null.")]
    public string RecordDefaultNull(
        [Description("Label value.")] string? label = null)
    {
        CapturedDefaultNull = label;
        CapturedDefaultNullWasSet = true;
        return "ok";
    }

    [AgentFunction]
    [Description("Records a label with a nullable default of \"x\".")]
    public string RecordDefaultLiteral(
        [Description("Label value.")] string? label = "x")
    {
        CapturedDefaultLiteral = label;
        return "ok";
    }
}
