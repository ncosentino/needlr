namespace NexusLabs.Needlr.AgentFramework.Diagnostics;

/// <summary>
/// DI-registered singleton that holds the <see cref="IToolCallCollector"/> instance.
/// The diagnostics middleware sets the real collector during factory construction; consumers
/// resolve this to access it. Unlike a static holder, each DI container has its own instance,
/// making it testable and thread-safe across containers.
/// </summary>
[DoNotAutoRegister]
public sealed class ToolCallCollectorHolder : IToolCallCollector
{
    private IToolCallCollector _inner = NullToolCallCollector.Instance;

    /// <summary>
    /// Gets whether a real (non-null) collector has been installed via
    /// <see cref="SetCollector"/>. When <see langword="true"/>,
    /// <c>UsingDiagnostics()</c> has already wired a diagnostics middleware
    /// onto the chat client pipeline and external consumers should not install
    /// a duplicate.
    /// </summary>
    internal bool HasRealCollector => _inner is not NullToolCallCollector;

    /// <summary>Sets the real collector. Called by <c>UsingDiagnostics()</c> during factory construction.</summary>
    internal void SetCollector(IToolCallCollector collector)
    {
        ArgumentNullException.ThrowIfNull(collector);
        _inner = collector;
    }

    /// <inheritdoc />
    public IReadOnlyList<ToolCallDiagnostics> DrainToolCalls() =>
        _inner.DrainToolCalls();
}
