using System.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Owns the <see cref="ActivitySource"/> used to mint Langfuse scenario spans.
/// </summary>
/// <remarks>
/// An <see cref="ActivitySource"/> is a process-global telemetry primitive matched by name by
/// the OpenTelemetry SDK (<c>AddSource</c>) and by <see cref="ActivityListener"/> in tests, so a
/// single shared instance is the idiomatic pattern rather than shared mutable state. It is kept
/// <see langword="internal"/> and is the only static in this package.
/// </remarks>
internal static class LangfuseActivitySource
{
    /// <summary>
    /// The name of the <see cref="ActivitySource"/> that emits Langfuse scenario spans. The
    /// export bootstrap subscribes to this name so scenario roots are forwarded to Langfuse.
    /// </summary>
    public const string Name = "NexusLabs.Needlr.AgentFramework.Langfuse";

    /// <summary>Gets the shared scenario <see cref="ActivitySource"/>.</summary>
    public static ActivitySource Source { get; } = new(Name);
}
