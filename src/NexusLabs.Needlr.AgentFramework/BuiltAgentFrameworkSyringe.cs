namespace NexusLabs.Needlr.AgentFramework;

/// <summary>
/// DI singleton that caches the resolved <see cref="AgentFrameworkSyringe"/>.
/// Used by both the <see cref="IAgentFactory"/> and <see cref="Progress.IProgressReporterFactory"/>
/// registrations so they observe identical configuration regardless of resolution order.
/// </summary>
/// <remarks>
/// The previous implementation captured sink types in a local closure mutated by the
/// <see cref="IAgentFactory"/> factory lambda, which created a resolve-order race with
/// <see cref="Progress.IProgressReporterFactory"/>. Routing both through this singleton
/// guarantees the configure delegate runs exactly once and both consumers see the same
/// built syringe.
/// </remarks>
[DoNotAutoRegister]
internal sealed class BuiltAgentFrameworkSyringe
{
    internal BuiltAgentFrameworkSyringe(AgentFrameworkSyringe value)
    {
        Value = value;
    }

    public AgentFrameworkSyringe Value { get; }
}
