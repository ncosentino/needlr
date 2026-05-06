namespace NexusLabs.Needlr.AgentFramework.GeneratedWrapper.Tests;

/// <summary>
/// DTO fixture exercising the three property shapes whose null-payload behavior changed
/// with the strict-extractor + DTO null-gate fix:
/// <list type="bullet">
/// <item><description><see cref="Foo"/> — non-nullable string with a C# init default.</description></item>
/// <item><description><see cref="Bar"/> — nullable string with no init default.</description></item>
/// <item><description><see cref="Count"/> — non-nullable value type with a C# init default.</description></item>
/// </list>
/// </summary>
public sealed class E2EDtoWithDefaults
{
    public string Foo { get; set; } = "default";
    public string? Bar { get; set; }
    public int Count { get; set; } = 5;
}
