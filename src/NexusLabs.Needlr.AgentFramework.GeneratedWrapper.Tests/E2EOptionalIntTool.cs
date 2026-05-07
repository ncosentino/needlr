using System.ComponentModel;

namespace NexusLabs.Needlr.AgentFramework.GeneratedWrapper.Tests;

[AgentFunctionGroup("e2e-optional-int")]
public sealed class E2EOptionalIntTool
{
    public sealed class Capture
    {
        public int? DefaultZero { get; set; }
        public int? DefaultFive { get; set; }
    }

    private readonly Capture _capture;

    public E2EOptionalIntTool(Capture capture)
    {
        _capture = capture;
    }

    [AgentFunction]
    [Description("Sets a max count with a default of zero.")]
    public string SetMaxDefaultZero(
        [Description("Maximum count.")] int max = 0)
    {
        _capture.DefaultZero = max;
        return "ok";
    }

    [AgentFunction]
    [Description("Sets a max count with a default of five.")]
    public string SetMaxDefaultFive(
        [Description("Maximum count.")] int max = 5)
    {
        _capture.DefaultFive = max;
        return "ok";
    }
}
