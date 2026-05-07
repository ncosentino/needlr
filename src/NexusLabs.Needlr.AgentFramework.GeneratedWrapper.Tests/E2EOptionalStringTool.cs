using System.ComponentModel;

namespace NexusLabs.Needlr.AgentFramework.GeneratedWrapper.Tests;

[AgentFunctionGroup("e2e-optional-string")]
public sealed class E2EOptionalStringTool
{
    public sealed class Capture
    {
        public string? DefaultNull { get; set; }
        public bool DefaultNullWasSet { get; set; }
        public string? DefaultLiteral { get; set; }
    }

    private readonly Capture _capture;

    public E2EOptionalStringTool(Capture capture)
    {
        _capture = capture;
    }

    [AgentFunction]
    [Description("Records a label with a nullable default of null.")]
    public string RecordDefaultNull(
        [Description("Label value.")] string? label = null)
    {
        _capture.DefaultNull = label;
        _capture.DefaultNullWasSet = true;
        return "ok";
    }

    [AgentFunction]
    [Description("Records a label with a nullable default of \"x\".")]
    public string RecordDefaultLiteral(
        [Description("Label value.")] string? label = "x")
    {
        _capture.DefaultLiteral = label;
        return "ok";
    }
}
