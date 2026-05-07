using System.ComponentModel;

namespace NexusLabs.Needlr.AgentFramework.GeneratedWrapper.Tests;

[AgentFunctionGroup("e2e-dto-defaults")]
public sealed class E2EDtoWithDefaultsTool
{
    public sealed class Capture
    {
        public E2EDtoWithDefaults? Value { get; set; }
    }

    private readonly Capture _capture;

    public E2EDtoWithDefaultsTool(Capture capture)
    {
        _capture = capture;
    }

    [AgentFunction]
    [Description("Records a DTO whose properties carry C# init defaults / nullability.")]
    public string Record(
        [Description("DTO with init defaults.")] E2EDtoWithDefaults dto)
    {
        _capture.Value = dto;
        return "ok";
    }
}
