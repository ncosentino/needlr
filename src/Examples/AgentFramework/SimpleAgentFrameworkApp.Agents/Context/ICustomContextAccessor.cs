using NexusLabs.Needlr.AgentFramework;

namespace SimpleAgentFrameworkApp.Agents;

/// <summary>
/// Demonstrates the <see cref="AsyncLocalScopedAttribute"/> source generator.
/// The generator emits a <c>CustomContextAccessor</c> class with AsyncLocal-backed
/// scoping — no hand-written boilerplate needed.
/// </summary>
[AsyncLocalScoped]
public interface ICustomContextAccessor
{
    /// <summary>Gets the current context for this async flow.</summary>
    string? Current { get; }

    /// <summary>Opens a scope with the given value.</summary>
    IDisposable BeginScope(string value);
}
