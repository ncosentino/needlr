using NexusLabs.Needlr.Generators;

namespace RecordConstructorOverloadExample;

/// <summary>
/// Describes an operation that may continue with an optional prepared access scope.
/// </summary>
/// <param name="Target">The target being processed.</param>
/// <param name="Operation">The operation identifier.</param>
/// <param name="PlanRevision">The append-only plan revision.</param>
/// <param name="NativeCapabilities">Capabilities available without preparation.</param>
/// <param name="PreparedCapabilities">Capabilities available after preparation.</param>
/// <param name="PreparedAdapters">The adapters used to prepare access.</param>
/// <param name="PreparedBytes">The number of bytes prepared in advance.</param>
public sealed partial record PreparedContinuationRequest(
    string Target,
    string Operation,
    int PlanRevision,
    IReadOnlyList<string> NativeCapabilities,
    IReadOnlyList<string> PreparedCapabilities,
    IReadOnlyList<string> PreparedAdapters,
    long PreparedBytes)
{
    /// <summary>
    /// Gets the exact prepared range, or <see langword="null"/> when the request uses
    /// the complete target.
    /// </summary>
    [RecordConstructorOverloadParameter]
    [ConstructorGuard(ConstructorGuardKind.NotNull)]
    public PreparedAccessScope? PreparedScope { get; init; }
}
