using System.ComponentModel;

namespace NexusLabs.Needlr.AgentFramework.GeneratedWrapper.Tests;

[AgentFunctionGroup("e2e-dto-defaults")]
public sealed class E2EDtoWithDefaultsTool
{
    public static E2EDtoWithDefaults? Captured { get; set; }

    [AgentFunction]
    [Description("Records a DTO whose properties carry C# init defaults / nullability.")]
    public string Record(
        [Description("DTO with init defaults.")] E2EDtoWithDefaults dto)
    {
        Captured = dto;
        return "ok";
    }
}
