namespace NexusLabs.Needlr.AgentFramework.Diagnostics;

/// <summary>
/// Default <see cref="IInFlightAgentDiagnosticsAccessor"/> that delegates to the
/// AsyncLocal-scoped <see cref="AgentRunDiagnosticsBuilder"/>. Stateless; safe
/// to register as a singleton.
/// </summary>
[DoNotAutoRegister]
internal sealed class InFlightAgentDiagnosticsAccessor : IInFlightAgentDiagnosticsAccessor
{
    /// <inheritdoc />
    public IAgentRunDiagnostics? Current => AgentRunDiagnosticsBuilder.GetCurrent()?.Build();
}
