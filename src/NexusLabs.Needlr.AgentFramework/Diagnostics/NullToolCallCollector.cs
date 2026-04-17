namespace NexusLabs.Needlr.AgentFramework.Diagnostics;

/// <summary>
/// No-op <see cref="IToolCallCollector"/> used when diagnostics are not configured.
/// </summary>
[DoNotAutoRegister]
internal sealed class NullToolCallCollector : IToolCallCollector
{
    internal static readonly NullToolCallCollector Instance = new();

    public IReadOnlyList<ToolCallDiagnostics> DrainToolCalls() => [];
}
