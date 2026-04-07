using NexusLabs.Needlr.AgentFramework;

namespace SimpleAgentFrameworkApp.Agents;

/// <summary>
/// Demonstrates <see cref="AsyncLocalScopedAttribute"/> with <c>Mutable = true</c>.
/// The generated implementation uses a mutable holder object so that values set from
/// child async flows (middleware) are visible to the parent scope.
/// The generated class also includes an <c>internal void Set(T value)</c> method.
/// </summary>
[AsyncLocalScoped(Mutable = true)]
public interface IMutableStateAccessor
{
    /// <summary>Gets the current state value.</summary>
    int? Current { get; }

    /// <summary>Opens a new capture scope.</summary>
    IDisposable BeginCapture();
}
