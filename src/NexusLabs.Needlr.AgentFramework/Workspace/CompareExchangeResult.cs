namespace NexusLabs.Needlr.AgentFramework.Workspace;

/// <summary>Result data for a successful <see cref="IWorkspace.TryCompareExchange"/> call.</summary>
/// <param name="Exchanged">Whether the content was swapped.</param>
/// <param name="Reason">
/// Why the exchange did not happen when <paramref name="Exchanged"/> is
/// <see langword="false"/> (e.g., "content mismatch"). <see langword="null"/>
/// when the exchange succeeded.
/// </param>
public sealed record CompareExchangeResult(bool Exchanged, string? Reason);
