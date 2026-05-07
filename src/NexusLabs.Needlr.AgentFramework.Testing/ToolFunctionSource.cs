namespace NexusLabs.Needlr.AgentFramework.Testing;

/// <summary>
/// Identifies which discovery path produced an <see cref="Microsoft.Extensions.AI.AIFunction"/>
/// resolved by <see cref="ToolInvocationRunner"/>.
/// </summary>
/// <remarks>
/// Surfacing the source on every <see cref="ToolInvocationResult"/> lets tests assert that they
/// are exercising the source-generated wrapper path and not silently falling through to
/// reflection-based discovery — the production path uses the generated wrapper, so a test that
/// only ever hits the reflection branch can pass while the wrapper has bugs.
/// </remarks>
public enum ToolFunctionSource
{
    /// <summary>
    /// The function was produced by the source-generated <see cref="IAIFunctionProvider"/>
    /// registered via <see cref="AgentFrameworkGeneratedBootstrap.RegisterAIFunctionProvider"/>.
    /// This is the same path the production <see cref="Microsoft.Agents.AI.AIAgent"/> takes.
    /// </summary>
    Generated,

    /// <summary>
    /// The function was produced by reflection-based discovery via
    /// <see cref="Microsoft.Extensions.AI.AIFunctionFactory"/>. Used only when the runner has
    /// been explicitly opted into reflection fallback (which is incompatible with NativeAOT).
    /// </summary>
    Reflection,
}
