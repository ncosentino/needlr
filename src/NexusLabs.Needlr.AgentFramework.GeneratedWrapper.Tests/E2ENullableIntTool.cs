using System.ComponentModel;

namespace NexusLabs.Needlr.AgentFramework.GeneratedWrapper.Tests;

[AgentFunctionGroup("e2e-nullable-int")]
public sealed class E2ENullableIntTool
{
    public sealed class Capture
    {
        public int? DefaultNull { get; set; }
        public bool DefaultNullWasSet { get; set; }
        public int? DefaultFive { get; set; }
    }

    private readonly Capture _capture;

    public E2ENullableIntTool(Capture capture)
    {
        _capture = capture;
    }

    [AgentFunction]
    [Description("Records an offset with a nullable-int default of null.")]
    public string RecordDefaultNull(
        [Description("Offset value.")] int? offset = null)
    {
        _capture.DefaultNull = offset;
        _capture.DefaultNullWasSet = true;
        return "ok";
    }

    [AgentFunction]
    [Description("Records an offset with a nullable-int default of 5.")]
    public string RecordDefaultFive(
        [Description("Offset value.")] int? offset = 5)
    {
        _capture.DefaultFive = offset;
        return "ok";
    }
}
